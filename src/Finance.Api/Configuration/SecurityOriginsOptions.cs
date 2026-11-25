namespace Finance.Api.Configuration;

public sealed class SecurityOriginsOptions
{
    private readonly HashSet<string> _allowedOrigins;

    public SecurityOriginsOptions(IEnumerable<string> origins)
    {
        _allowedOrigins = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (string raw in origins)
        {
            if (TryNormalizeOrigin(raw, out string normalized))
            {
                _allowedOrigins.Add(normalized);
            }
        }
    }

    public string[] AllowedOrigins => _allowedOrigins.ToArray();

    public bool IsAllowedOrigin(string? rawOriginOrUrl, HttpRequest request)
    {
        if (!TryNormalizeOrigin(rawOriginOrUrl, out string normalized))
        {
            return false;
        }

        if (_allowedOrigins.Contains(normalized))
        {
            return true;
        }

        if (request.Host.HasValue)
        {
            string selfOrigin = $"{request.Scheme}://{request.Host.Value}";
            if (TryNormalizeOrigin(selfOrigin, out string normalizedSelf) &&
                string.Equals(normalized, normalizedSelf, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public static string[] ResolveConfiguredOrigins(IConfiguration configuration)
    {
        IEnumerable<string> fromSection = configuration.GetSection("AllowedOrigins").Get<string[]>() ?? [];
        string? envOrigins = configuration["ALLOWED_ORIGINS"];
        IEnumerable<string> fromEnv = string.IsNullOrWhiteSpace(envOrigins)
            ? []
            : envOrigins.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return fromSection
            .Concat(fromEnv)
            .Where(o => !string.IsNullOrWhiteSpace(o))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool TryNormalizeOrigin(string? value, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out Uri? uri))
            return false;

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        normalized = uri.GetLeftPart(UriPartial.Authority).TrimEnd('/');
        return true;
    }
}
