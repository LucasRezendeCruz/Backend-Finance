using Finance.Application.Common.Interfaces;
using Finance.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Finance.Infrastructure.Persistence.Repositories;

internal sealed class RecurrenceRepository(FinanceDbContext dbContext) : IRecurrenceRepository
{
    public async Task<IReadOnlyList<RecurrenceRule>> GetByUserAsync(
        Guid userId,
        bool includeInactive,
        CancellationToken ct = default)
    {
        IQueryable<RecurrenceRule> query = dbContext.RecurrenceRules
            .AsNoTracking()
            .Where(r => r.UserId == userId);

        if (!includeInactive)
            query = query.Where(r => r.IsActive);

        return await query
            .OrderByDescending(r => r.IsActive)
            .ThenBy(r => r.Description)
            .ToListAsync(ct);
    }

    public Task<RecurrenceRule?> GetByIdAsync(Guid userId, Guid ruleId, CancellationToken ct = default)
        => dbContext.RecurrenceRules.FirstOrDefaultAsync(r => r.UserId == userId && r.Id == ruleId, ct);

    public async Task AddAsync(RecurrenceRule rule, CancellationToken ct = default)
        => await dbContext.RecurrenceRules.AddAsync(rule, ct);

    public Task<bool> OccurrenceExistsAsync(
        Guid userId,
        Guid recurrenceRuleId,
        DateOnly transactionDate,
        CancellationToken ct = default)
        => dbContext.Transactions.AnyAsync(
            t => t.UserId == userId &&
                 t.RecurrenceRuleId == recurrenceRuleId &&
                 t.TransactionDate == transactionDate,
            ct);

    public Task<int> CountOccurrencesAsync(
        Guid userId,
        Guid recurrenceRuleId,
        CancellationToken ct = default)
        => dbContext.Transactions.CountAsync(
            t => t.UserId == userId && t.RecurrenceRuleId == recurrenceRuleId,
            ct);
}
