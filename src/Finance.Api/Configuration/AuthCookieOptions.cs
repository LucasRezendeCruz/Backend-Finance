namespace Finance.Api.Configuration;

public sealed class AuthCookieOptions
{
    public const string SectionName = "Security:AuthCookie";

    public string Name { get; set; } = "finance_refresh_token";
    public string Path { get; set; } = "/api/v1/auth";
    public SameSiteMode SameSite { get; set; } = SameSiteMode.Strict;
}
