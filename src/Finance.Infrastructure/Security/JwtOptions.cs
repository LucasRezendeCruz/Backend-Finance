using Microsoft.Extensions.Configuration;

namespace Finance.Infrastructure.Security;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public const int MinimumSecretKeyBytes = 32;

    public string SecretKey { get; set; } = string.Empty;
    public string Issuer { get; set; } = "Finance.Api";
    public string Audience { get; set; } = "Finance.Clients";
    public int AccessTokenMinutes { get; set; } = 10;
    public int WebRefreshTokenDays { get; set; } = 7;
    public int MobileRefreshTokenDays { get; set; } = 30;

    public static string? ResolveSecretKey(IConfiguration configuration)
    {
        string? configured = configuration["Jwt:SecretKey"];
        if (!string.IsNullOrWhiteSpace(configured))
            return configured;

        string? envFallback = configuration["JWT_SECRET_KEY"];
        return string.IsNullOrWhiteSpace(envFallback) ? null : envFallback;
    }
}
