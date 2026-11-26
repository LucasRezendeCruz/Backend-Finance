using Finance.Application.Common.Interfaces;
using Finance.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Finance.Api.Tests.Infrastructure;

public sealed class FakeDateTimeProvider(DateTimeOffset initialUtcNow) : IDateTimeProvider
{
    public DateTimeOffset UtcNow { get; private set; } = initialUtcNow;

    public void Advance(TimeSpan delta) => UtcNow = UtcNow.Add(delta);

    public void SetUtcNow(DateTimeOffset utcNow) => UtcNow = utcNow;
}

public sealed record SentTelegramMessage(
    long ChatId,
    string Text,
    IReadOnlyList<TelegramInlineButton>? InlineButtons);

public sealed class FakeTelegramBotClient(string webhookSecret, string botUsername = "RazoTestBot") : ITelegramBotClient
{
    public List<SentTelegramMessage> SentMessages { get; } = [];
    public List<(string CallbackQueryId, string? Text)> AnsweredCallbacks { get; } = [];

    public bool IsConfigured => true;
    public string Mode => "Webhook";
    public string? ConfiguredBotUsername => botUsername;
    public string? WebhookSecret => webhookSecret;

    public Task SendMessageAsync(
        long chatId,
        string text,
        IReadOnlyList<TelegramInlineButton>? inlineButtons = null,
        CancellationToken ct = default)
    {
        SentMessages.Add(new SentTelegramMessage(chatId, text, inlineButtons));
        return Task.CompletedTask;
    }

    public Task AnswerCallbackQueryAsync(string callbackQueryId, string? text = null, CancellationToken ct = default)
    {
        AnsweredCallbacks.Add((callbackQueryId, text));
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<TelegramBotUpdate>> GetUpdatesAsync(long? offset, int timeoutSeconds, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<TelegramBotUpdate>>([]);

    public Task<string?> ResolveBotUsernameAsync(CancellationToken ct = default)
        => Task.FromResult<string?>(botUsername);
}

public sealed class FinanceApiFactory(
    int authStrictPermitLimit = 1000,
    int authStrictWindowSeconds = 60,
    int apiAuthenticatedPermitLimit = 1000,
    int apiAuthenticatedWindowSeconds = 60) : WebApplicationFactory<Program>
{
    public const string AllowedWebOrigin = "http://localhost:5173";
    public const string TestJwtSecret = "TEST_ONLY_SIGNING_KEY_AT_LEAST_32_CHARS_LONG_123456";
    public const string TestTelegramWebhookSecret = "TEST_TELEGRAM_WEBHOOK_SECRET_987654321";

    private readonly SqliteConnection _connection = CreateOpenConnection();

    public FakeDateTimeProvider Clock { get; } =
        new(new DateTimeOffset(2026, 11, 24, 15, 0, 0, TimeSpan.Zero));

    public FakeTelegramBotClient TelegramBot { get; } = new(TestTelegramWebhookSecret);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        var overrides = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "DataSource=:memory:",
            ["Jwt:SecretKey"] = TestJwtSecret,
            ["Jwt:Issuer"] = "Finance.Api",
            ["Jwt:Audience"] = "Finance.Clients",
            ["Jwt:AccessTokenMinutes"] = "10",
            ["Jwt:WebRefreshTokenDays"] = "7",
            ["Jwt:MobileRefreshTokenDays"] = "30",
            ["AllowedOrigins:0"] = AllowedWebOrigin,
            ["Security:Argon2:MemorySizeKb"] = "4096",
            ["Security:Argon2:Iterations"] = "1",
            ["Security:Argon2:DegreeOfParallelism"] = "1",
            ["Security:RateLimiting:AuthStrictPermitLimit"] = authStrictPermitLimit.ToString(),
            ["Security:RateLimiting:AuthStrictWindowSeconds"] = authStrictWindowSeconds.ToString(),
            ["Security:RateLimiting:ApiAuthenticatedPermitLimit"] = apiAuthenticatedPermitLimit.ToString(),
            ["Security:RateLimiting:ApiAuthenticatedWindowSeconds"] = apiAuthenticatedWindowSeconds.ToString()
        };

        foreach ((string key, string? value) in overrides)
        {
            builder.UseSetting(key, value);
        }

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(overrides);
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<FinanceDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<FinanceDbContext>>();
            services.RemoveAll<FinanceDbContext>();

            services.AddDbContext<FinanceDbContext>(options => options.UseSqlite(_connection));

            services.RemoveAll<IDateTimeProvider>();
            services.AddSingleton<IDateTimeProvider>(Clock);

            services.RemoveAll<ITelegramBotClient>();
            services.AddSingleton<ITelegramBotClient>(TelegramBot);
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        IHost host = base.CreateHost(builder);

        using IServiceScope scope = host.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
        dbContext.Database.EnsureCreated();

        return host;
    }

    public async Task WithDbContextAsync(Func<FinanceDbContext, Task> action)
    {
        using IServiceScope scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
        await action(dbContext);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection.Dispose();
        }
    }

    private static SqliteConnection CreateOpenConnection()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        return connection;
    }
}
