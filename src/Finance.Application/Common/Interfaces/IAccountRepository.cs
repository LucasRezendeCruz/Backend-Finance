using Finance.Domain.Entities;

namespace Finance.Application.Common.Interfaces;

public interface IAccountRepository
{
    Task<IReadOnlyList<Account>> GetByUserAsync(Guid userId, bool includeArchived, CancellationToken ct = default);
    Task<Account?> GetByIdAsync(Guid userId, Guid accountId, CancellationToken ct = default);
    Task AddAsync(Account account, CancellationToken ct = default);
}
