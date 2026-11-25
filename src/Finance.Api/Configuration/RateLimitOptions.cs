namespace Finance.Api.Configuration;

public sealed class RateLimitOptions
{
    public const string SectionName = "Security:RateLimiting";

    public int AuthStrictPermitLimit { get; set; } = 10;
    public int AuthStrictWindowSeconds { get; set; } = 60;
    public int ApiAuthenticatedPermitLimit { get; set; } = 120;
    public int ApiAuthenticatedWindowSeconds { get; set; } = 60;
}
