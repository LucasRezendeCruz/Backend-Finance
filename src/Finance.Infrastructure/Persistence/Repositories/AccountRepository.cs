using Finance.Application.Common.Interfaces;
using Finance.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Finance.Infrastructure.Persistence.Repositories;

internal sealed class AccountRepository(FinanceDbContext dbContext) : IAccountRepository
{
    public async Task<IReadOnlyList<Account>> GetByUserAsync(
        Guid userId,
        bool includeArchived,
        CancellationToken ct = default)
    {
        IQueryable<Account> query = dbContext.Accounts
            .AsNoTracking()
            .Where(a => a.UserId == userId);

        if (!includeArchived)
            query = query.Where(a => !a.IsArchived);

        return await query
            .OrderBy(a => a.IsArchived)
            .ThenBy(a => a.Name)
            .ToListAsync(ct);
    }

    public Task<Account?> GetByIdAsync(Guid userId, Guid accountId, CancellationToken ct = default)
        => dbContext.Accounts.FirstOrDefaultAsync(a => a.UserId == userId && a.Id == accountId, ct);

    public async Task AddAsync(Account account, CancellationToken ct = default)
        => await dbContext.Accounts.AddAsync(account, ct);
}
