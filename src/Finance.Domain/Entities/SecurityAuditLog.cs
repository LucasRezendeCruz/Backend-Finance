using Finance.Domain.Common;
using Finance.Domain.Enums;

namespace Finance.Domain.Entities;

public sealed class SecurityAuditLog : Entity
{
    public Guid? UserId { get; private set; }
    public SecurityAuditEventType EventType { get; private set; }
    public string IpAddress { get; private set; } = string.Empty;
    public string UserAgent { get; private set; } = string.Empty;
    public string? MetadataJson { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    private SecurityAuditLog() { }

    public SecurityAuditLog(
        Guid? userId,
        SecurityAuditEventType eventType,
        string ipAddress,
        string userAgent,
        DateTimeOffset createdAtUtc,
        string? metadataJson = null)
    {
        UserId = userId;
        EventType = eventType;
        IpAddress = ipAddress?.Trim() ?? string.Empty;
        UserAgent = userAgent?.Trim() ?? string.Empty;
        MetadataJson = metadataJson;
        CreatedAtUtc = createdAtUtc;
    }
}
