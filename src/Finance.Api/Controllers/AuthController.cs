using System.Text.Json.Serialization;
using Finance.Api.Configuration;
using Finance.Application.Auth;
using Finance.Application.Common.Exceptions;
using Finance.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Finance.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(
    IAuthService authService,
    IOptions<AuthCookieOptions> cookieOptionsAccessor,
    SecurityOriginsOptions securityOrigins,
    IHostEnvironment hostEnvironment) : ControllerBase
{
    private const string ClientTypeHeaderName = "X-Client-Type";
    private readonly AuthCookieOptions _cookieOptions = cookieOptionsAccessor.Value;

    public sealed record RegisterRequest(
        string Name,
        string Email,
        string Password,
        ClientType ClientType = ClientType.Web,
        string? DeviceName = null);

    public sealed record LoginRequest(
        string Email,
        string Password,
        ClientType ClientType = ClientType.Web,
        string? DeviceName = null);

    public sealed record RefreshRequest(
        string? RefreshToken = null,
        ClientType? ClientType = null,
        string? DeviceName = null);

    public sealed record LogoutRequest(
        string? RefreshToken = null,
        ClientType? ClientType = null);

    public sealed record AuthResponse(
        AuthenticatedUserDto User,
        string AccessToken,
        DateTimeOffset AccessTokenExpiresAtUtc,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        string? RefreshToken = null,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        DateTimeOffset? RefreshTokenExpiresAtUtc = null);

    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-strict")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Register(
        [FromBody] RegisterRequest request,
        CancellationToken ct)
    {
        AuthSessionResult result = await authService.RegisterAsync(
            new RegisterCommand(
                request.Name,
                request.Email,
                request.Password,
                request.ClientType,
                request.DeviceName),
            ct);

        return StatusCode(StatusCodes.Status201Created, ApplySessionDelivery(result));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-strict")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request,
        CancellationToken ct)
    {
        AuthSessionResult result = await authService.LoginAsync(
            new LoginCommand(
                request.Email,
                request.Password,
                request.ClientType,
                request.DeviceName),
            ct);

        return Ok(ApplySessionDelivery(result));
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-strict")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Refresh(
        [FromBody] RefreshRequest? request,
        CancellationToken ct)
    {
        bool hasWebCookie = Request.Cookies.TryGetValue(_cookieOptions.Name, out string? cookieToken) &&
            !string.IsNullOrWhiteSpace(cookieToken);

        string? clientTypeHeader = Request.Headers[ClientTypeHeaderName].FirstOrDefault();
        bool isWebFlow = hasWebCookie ||
            string.Equals(clientTypeHeader, "web", StringComparison.OrdinalIgnoreCase) ||
            request?.ClientType == ClientType.Web;

        if (isWebFlow)
        {
            ValidateWebCsrf(clientTypeHeader, hasWebCookie);

            try
            {
                AuthSessionResult webResult = await authService.RefreshAsync(
                    new RefreshCommand(
                        cookieToken ?? string.Empty,
                        ClientType.Web,
                        request?.DeviceName),
                    ct);

                return Ok(ApplySessionDelivery(webResult));
            }
            catch (AppException ex) when (ex.StatusCode == StatusCodes.Status401Unauthorized)
            {
                ClearRefreshCookie();
                throw;
            }
        }

        AuthSessionResult mobileResult = await authService.RefreshAsync(
            new RefreshCommand(
                request?.RefreshToken ?? string.Empty,
                ClientType.Mobile,
                request?.DeviceName),
            ct);

        return Ok(ApplySessionDelivery(mobileResult));
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-strict")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Logout(
        [FromBody] LogoutRequest? request,
        CancellationToken ct)
    {
        bool hasWebCookie = Request.Cookies.TryGetValue(_cookieOptions.Name, out string? cookieToken) &&
            !string.IsNullOrWhiteSpace(cookieToken);

        string? clientTypeHeader = Request.Headers[ClientTypeHeaderName].FirstOrDefault();
        bool isWebFlow = hasWebCookie ||
            string.Equals(clientTypeHeader, "web", StringComparison.OrdinalIgnoreCase) ||
            request?.ClientType == ClientType.Web;

        if (isWebFlow)
        {
            ValidateWebCsrf(clientTypeHeader, hasWebCookie);
            ClearRefreshCookie();
            await authService.LogoutAsync(new LogoutCommand(cookieToken, ClientType.Web), ct);
            return NoContent();
        }

        await authService.LogoutAsync(
            new LogoutCommand(request?.RefreshToken, ClientType.Mobile),
            ct);

        return NoContent();
    }

    [HttpGet("sessions")]
    [Authorize]
    [EnableRateLimiting("api-authenticated")]
    [ProducesResponseType(typeof(IReadOnlyList<SessionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> GetSessions(CancellationToken ct)
        => Ok(await authService.GetActiveSessionsAsync(ct));

    [HttpDelete("sessions/{id:guid}")]
    [Authorize]
    [EnableRateLimiting("api-authenticated")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> RevokeSession(Guid id, CancellationToken ct)
    {
        RevokeSessionResult result = await authService.RevokeSessionAsync(id, ct);

        if (result.RevokedCurrentSession && Request.Cookies.ContainsKey(_cookieOptions.Name))
        {
            ClearRefreshCookie();
        }

        return NoContent();
    }

    private AuthResponse ApplySessionDelivery(AuthSessionResult result)
    {
        if (result.ClientType == ClientType.Web)
        {
            AppendRefreshCookie(result.RefreshToken, result.RefreshTokenExpiresAtUtc);
            return new AuthResponse(result.User, result.AccessToken, result.AccessTokenExpiresAtUtc);
        }

        return new AuthResponse(
            result.User,
            result.AccessToken,
            result.AccessTokenExpiresAtUtc,
            result.RefreshToken,
            result.RefreshTokenExpiresAtUtc);
    }

    private void ValidateWebCsrf(string? clientTypeHeader, bool hasWebCookie)
    {
        if (!string.Equals(clientTypeHeader, "web", StringComparison.OrdinalIgnoreCase))
        {
            throw AppException.Forbidden(
                "csrf_validation_failed",
                "Cabeçalho de segurança X-Client-Type: web obrigatório.");
        }

        string? origin = Request.Headers.Origin.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(origin))
        {
            if (!securityOrigins.IsAllowedOrigin(origin, Request))
            {
                throw AppException.Forbidden(
                    "csrf_validation_failed",
                    "Origem da requisição não autorizada.");
            }

            return;
        }

        if (hasWebCookie)
        {
            string? referer = Request.Headers.Referer.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(referer) && !securityOrigins.IsAllowedOrigin(referer, Request))
            {
                throw AppException.Forbidden(
                    "csrf_validation_failed",
                    "Origem da requisição não autorizada.");
            }
        }
    }

    private void AppendRefreshCookie(string rawRefreshToken, DateTimeOffset expiresAtUtc)
    {
        Response.Cookies.Append(
            _cookieOptions.Name,
            rawRefreshToken,
            BuildCookieOptions(expiresAtUtc));
    }

    private void ClearRefreshCookie()
    {
        Response.Cookies.Delete(_cookieOptions.Name, BuildCookieOptions(expiresAtUtc: null));
    }

    private CookieOptions BuildCookieOptions(DateTimeOffset? expiresAtUtc)
    {
        bool isLocalHttpDev = (hostEnvironment.IsDevelopment() || hostEnvironment.IsEnvironment("Testing")) &&
            !Request.IsHttps;

        return new CookieOptions
        {
            HttpOnly = true,
            Secure = !isLocalHttpDev,
            SameSite = _cookieOptions.SameSite,
            Path = _cookieOptions.Path,
            IsEssential = true,
            Expires = expiresAtUtc
        };
    }
}
