namespace Finance.Application.Security;

public interface ISecurityAuditService
{
    Task<PagedAuditLogsDto> GetAuditLogsAsync(
        int page,
        int pageSize,
        bool alertsOnly,
        CancellationToken ct = default);
}
