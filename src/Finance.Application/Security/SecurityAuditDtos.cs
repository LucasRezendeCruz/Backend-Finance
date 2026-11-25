using System.Text.Json;

namespace Finance.Application.Security;

public sealed record SecurityAuditLogItemDto(
    Guid Id,
    string EventType,
    string IpAddress,
    string UserAgent,
    JsonElement? Metadata,
    DateTimeOffset CreatedAtUtc);

public sealed record PagedAuditLogsDto(
    IReadOnlyList<SecurityAuditLogItemDto> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
