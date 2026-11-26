using System.Security.Claims;
using Finance.Application.Common.Interfaces;
using Finance.Domain.Enums;

namespace Finance.Api.Services;

internal sealed class HttpCurrentUserService(IHttpContextAccessor httpContextAccessor)
    : ICurrentUserService, ICurrentUserOverride
{
    private Guid? _overrideUserId;
    private string? _overrideUserAgent;

    public Guid? UserId
    {
        get
        {
            string? sub = httpContextAccessor.HttpContext?.User.FindFirstValue("sub")
                ?? httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (Guid.TryParse(sub, out Guid userId))
                return userId;

            return _overrideUserId;
        }
    }

    public Guid? SessionFamilyId
    {
        get
        {
            string? sid = httpContextAccessor.HttpContext?.User.FindFirstValue("sid");
            return Guid.TryParse(sid, out Guid familyId) ? familyId : null;
        }
    }

    public ClientType? ClientType
    {
        get
        {
            string? raw = httpContextAccessor.HttpContext?.User.FindFirstValue("client_type");
            return Enum.TryParse<ClientType>(raw, ignoreCase: true, out ClientType parsed)
                ? parsed
                : null;
        }
    }

    public string IpAddress
    {
        get
        {
            string? ip = httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
            return string.IsNullOrWhiteSpace(ip) ? "127.0.0.1" : ip;
        }
    }

    public string UserAgent
        => _overrideUserAgent ?? httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString() ?? string.Empty;

    public IDisposable RunAs(Guid userId, string userAgent)
    {
        Guid? previousUserId = _overrideUserId;
        string? previousUserAgent = _overrideUserAgent;

        _overrideUserId = userId;
        _overrideUserAgent = userAgent;

        return new RestoreScope(this, previousUserId, previousUserAgent);
    }

    private sealed class RestoreScope(
        HttpCurrentUserService owner,
        Guid? previousUserId,
        string? previousUserAgent) : IDisposable
    {
        public void Dispose()
        {
            owner._overrideUserId = previousUserId;
            owner._overrideUserAgent = previousUserAgent;
        }
    }
}
