using Finance.Domain.Entities;

namespace Finance.Application.Common.Interfaces;

public interface ISecurityAuditRepository
{
    Task AddAsync(SecurityAuditLog auditLog, CancellationToken ct = default);

    Task<(IReadOnlyList<SecurityAuditLog> Items, int TotalCount)> GetUserAuditLogsAsync(
        Guid userId,
        int page,
        int pageSize,
        bool alertsOnly,
        CancellationToken ct = default);
}
