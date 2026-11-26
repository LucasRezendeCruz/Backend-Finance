using Finance.Domain.Entities;

namespace Finance.Application.Common.Interfaces;

public interface ITelegramRepository
{
    Task<TelegramLink?> GetLinkByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<TelegramLink?> GetLinkByChatIdAsync(long telegramChatId, CancellationToken ct = default);
    Task AddLinkAsync(TelegramLink link, CancellationToken ct = default);
    void RemoveLink(TelegramLink link);

    Task<TelegramLinkToken?> GetTokenByHashAsync(string tokenHash, CancellationToken ct = default);
    Task<IReadOnlyList<TelegramLinkToken>> GetUnconsumedTokensByUserAsync(Guid userId, CancellationToken ct = default);
    Task AddTokenAsync(TelegramLinkToken token, CancellationToken ct = default);
}
