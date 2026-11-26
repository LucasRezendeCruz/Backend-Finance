using Finance.Application.Common.Interfaces;
using Finance.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Finance.Infrastructure.Persistence.Repositories;

internal sealed class TelegramRepository(FinanceDbContext dbContext) : ITelegramRepository
{
    public Task<TelegramLink?> GetLinkByUserIdAsync(Guid userId, CancellationToken ct = default)
        => dbContext.TelegramLinks.FirstOrDefaultAsync(x => x.UserId == userId, ct);

    public Task<TelegramLink?> GetLinkByChatIdAsync(long telegramChatId, CancellationToken ct = default)
        => dbContext.TelegramLinks.FirstOrDefaultAsync(x => x.TelegramChatId == telegramChatId, ct);

    public async Task AddLinkAsync(TelegramLink link, CancellationToken ct = default)
        => await dbContext.TelegramLinks.AddAsync(link, ct);

    public void RemoveLink(TelegramLink link)
        => dbContext.TelegramLinks.Remove(link);

    public Task<TelegramLinkToken?> GetTokenByHashAsync(string tokenHash, CancellationToken ct = default)
        => dbContext.TelegramLinkTokens.FirstOrDefaultAsync(x => x.TokenHash == tokenHash, ct);

    public async Task<IReadOnlyList<TelegramLinkToken>> GetUnconsumedTokensByUserAsync(
        Guid userId,
        CancellationToken ct = default)
        => await dbContext.TelegramLinkTokens
            .Where(x => x.UserId == userId && x.ConsumedAtUtc == null)
            .ToListAsync(ct);

    public async Task AddTokenAsync(TelegramLinkToken token, CancellationToken ct = default)
        => await dbContext.TelegramLinkTokens.AddAsync(token, ct);
}
