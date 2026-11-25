using System.Security.Claims;
using Finance.Application.Common.Interfaces;
using Finance.Domain.Enums;

namespace Finance.Api.Services;

internal sealed class HttpCurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    public Guid? UserId
    {
        get
        {
            string? sub = httpContextAccessor.HttpContext?.User.FindFirstValue("sub")
                ?? httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);

            return Guid.TryParse(sub, out Guid userId) ? userId : null;
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
        => httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString() ?? string.Empty;
}
