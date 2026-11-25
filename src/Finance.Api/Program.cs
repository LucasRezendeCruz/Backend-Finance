using System.Globalization;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Finance.Api.Configuration;
using Finance.Api.Middleware;
using Finance.Api.Services;
using Finance.Application;
using Finance.Application.Common.Interfaces;
using Finance.Infrastructure;
using Finance.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

if (EF.IsDesignTime && !builder.Environment.IsDevelopment())
{
    builder.Configuration
        .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: false)
        .AddUserSecrets(Assembly.GetExecutingAssembly(), optional: true)
        .AddEnvironmentVariables();
}

builder.Services.AddSingleton(sp =>
    new SecurityOriginsOptions(
        SecurityOriginsOptions.ResolveConfiguredOrigins(sp.GetRequiredService<IConfiguration>())));

builder.Services.Configure<AuthCookieOptions>(builder.Configuration.GetSection(AuthCookieOptions.SectionName));
builder.Services.Configure<RateLimitOptions>(builder.Configuration.GetSection(RateLimitOptions.SectionName));

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, HttpCurrentUserService>();

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();

builder.Services
    .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IConfiguration, IHostEnvironment, IDateTimeProvider>((options, configuration, environment, dateTimeProvider) =>
    {
        string jwtSecretKey = ResolveValidatedJwtSecret(configuration, environment);
        string jwtIssuer = configuration[$"{JwtOptions.SectionName}:Issuer"] ?? "Finance.Api";
        string jwtAudience = configuration[$"{JwtOptions.SectionName}:Audience"] ?? "Finance.Clients";

        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecretKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(15),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            LifetimeValidator = (notBefore, expires, _, validationParameters) =>
            {
                DateTime utcNow = dateTimeProvider.UtcNow.UtcDateTime;
                TimeSpan skew = validationParameters.ClockSkew;

                if (notBefore.HasValue && notBefore.Value > utcNow.Add(skew))
                    return false;

                if (!expires.HasValue || expires.Value <= utcNow.Subtract(skew))
                    return false;

                return true;
            }
        };

        options.Events = new JwtBearerEvents
        {
            OnChallenge = async context =>
            {
                context.HandleResponse();

                if (context.Response.HasStarted)
                    return;

                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.Headers.WWWAuthenticate = "Bearer";

                var problem = new ProblemDetails
                {
                    Status = StatusCodes.Status401Unauthorized,
                    Title = "Não autorizado",
                    Detail = "Token de acesso ausente, inválido ou expirado.",
                    Instance = context.Request.Path
                };
                problem.Extensions["code"] = "authentication_required";
                problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;

                await context.Response.WriteAsJsonAsync(problem, context.HttpContext.RequestAborted);
            }
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("auth-strict", httpContext =>
    {
        RateLimitOptions rateLimitOptions = httpContext.RequestServices
            .GetRequiredService<IOptions<RateLimitOptions>>()
            .Value;

        string ipKey = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: $"auth:{ipKey}",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = rateLimitOptions.AuthStrictPermitLimit,
                Window = TimeSpan.FromSeconds(rateLimitOptions.AuthStrictWindowSeconds),
                QueueLimit = 0,
                AutoReplenishment = true
            });
    });

    options.AddPolicy("api-authenticated", httpContext =>
    {
        RateLimitOptions rateLimitOptions = httpContext.RequestServices
            .GetRequiredService<IOptions<RateLimitOptions>>()
            .Value;

        string? sub = httpContext.User.FindFirstValue("sub")
            ?? httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

        string partitionKey = !string.IsNullOrWhiteSpace(sub)
            ? $"user:{sub}"
            : $"ip:{httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = rateLimitOptions.ApiAuthenticatedPermitLimit,
                Window = TimeSpan.FromSeconds(rateLimitOptions.ApiAuthenticatedWindowSeconds),
                QueueLimit = 0,
                AutoReplenishment = true
            });
    });

    options.OnRejected = async (context, cancellationToken) =>
    {
        int retryAfterSeconds = 60;
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan retryAfter))
        {
            retryAfterSeconds = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds));
        }

        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.Headers.RetryAfter = retryAfterSeconds.ToString(CultureInfo.InvariantCulture);

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = "Muitas requisições",
            Detail = "Limite de requisições atingido. Aguarde alguns instantes e tente novamente.",
            Instance = context.HttpContext.Request.Path
        };
        problem.Extensions["code"] = "rate_limit_exceeded";
        problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;

        await context.HttpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
    };
});

builder.Services.AddCors();
builder.Services
    .AddOptions<CorsOptions>()
    .Configure<SecurityOriginsOptions>((corsOptions, origins) =>
    {
        corsOptions.AddPolicy("StrictPolicy", policy =>
        {
            if (origins.AllowedOrigins.Length > 0)
            {
                policy.WithOrigins(origins.AllowedOrigins)
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            }
        });
    });

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new FlexibleEnumJsonConverterFactory());
        options.JsonSerializerOptions.Converters.Add(
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
    });

builder.Services.AddOpenApi();

var app = builder.Build();

_ = ResolveValidatedJwtSecret(app.Configuration, app.Environment);

app.UseExceptionHandler();
app.UseMiddleware<SecurityHeadersMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
else
{
    app.UseHsts();
}

if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing"))
{
    app.UseHttpsRedirection();
}

app.UseCors("StrictPolicy");
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();
app.MapControllers();

app.Run();

static string ResolveValidatedJwtSecret(IConfiguration configuration, IHostEnvironment environment)
{
    string? secret = JwtOptions.ResolveSecretKey(configuration);

    if (EF.IsDesignTime &&
        (string.IsNullOrWhiteSpace(secret) || Encoding.UTF8.GetByteCount(secret) < JwtOptions.MinimumSecretKeyBytes))
    {
        return "EF_DESIGN_TIME_EPHEMERAL_KEY_MIN_32_BYTES_UNUSED";
    }

    if (string.IsNullOrWhiteSpace(secret) ||
        Encoding.UTF8.GetByteCount(secret) < JwtOptions.MinimumSecretKeyBytes)
    {
        throw new InvalidOperationException(
            "Configure 'Jwt:SecretKey' (ou JWT_SECRET_KEY) com pelo menos 256 bits (32 bytes).");
    }

    if (!environment.IsDevelopment() &&
        !environment.IsEnvironment("Testing") &&
        (secret.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase) ||
         secret.Contains("DEV_ONLY", StringComparison.OrdinalIgnoreCase)))
    {
        throw new InvalidOperationException(
            "A chave JWT de placeholder não é permitida fora do ambiente de desenvolvimento.");
    }

    return secret;
}

public partial class Program;
