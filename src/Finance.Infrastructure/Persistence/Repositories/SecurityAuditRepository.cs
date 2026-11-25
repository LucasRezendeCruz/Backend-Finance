using Finance.Application.Common.Interfaces;
using Finance.Domain.Entities;
using Finance.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Finance.Infrastructure.Persistence.Repositories;

internal sealed class SecurityAuditRepository(FinanceDbContext dbContext) : ISecurityAuditRepository
{
    public async Task<(IReadOnlyList<SecurityAuditLog> Items, int TotalCount)> GetUserAuditLogsAsync(
        Guid userId,
        int page,
        int pageSize,
        bool alertsOnly,
        CancellationToken ct = default)
    {
        IQueryable<SecurityAuditLog> query = dbContext.SecurityAuditLogs
            .AsNoTracking()
            .Where(log => log.UserId == userId);

        if (alertsOnly)
        {
            query = query.Where(log =>
                log.EventType == SecurityAuditEventType.LoginFailed ||
                log.EventType == SecurityAuditEventType.LoginLocked ||
                log.EventType == SecurityAuditEventType.RefreshTokenReuseDetected ||
                log.EventType == SecurityAuditEventType.SessionRevoked);
        }

        int totalCount = await query.CountAsync(ct);

        List<SecurityAuditLog> items = await query
            .OrderByDescending(log => log.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }
}
