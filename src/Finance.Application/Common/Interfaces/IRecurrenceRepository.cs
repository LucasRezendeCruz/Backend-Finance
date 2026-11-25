using Finance.Domain.Entities;

namespace Finance.Application.Common.Interfaces;

public interface IRecurrenceRepository
{
    Task<IReadOnlyList<RecurrenceRule>> GetByUserAsync(Guid userId, bool includeInactive, CancellationToken ct = default);
    Task<RecurrenceRule?> GetByIdAsync(Guid userId, Guid ruleId, CancellationToken ct = default);
    Task AddAsync(RecurrenceRule rule, CancellationToken ct = default);
    Task<bool> OccurrenceExistsAsync(Guid userId, Guid recurrenceRuleId, DateOnly transactionDate, CancellationToken ct = default);
    Task<int> CountOccurrencesAsync(Guid userId, Guid recurrenceRuleId, CancellationToken ct = default);
}
