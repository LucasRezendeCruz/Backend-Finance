using Finance.Application.Common.Interfaces;

namespace Finance.Application.Telegram;

public interface ITelegramBotService
{
    Task<TelegramStatusDto> GetStatusAsync(CancellationToken ct = default);
    Task<TelegramLinkTokenResultDto> GenerateLinkTokenAsync(CancellationToken ct = default);
    Task<TelegramStatusDto> UpdatePreferencesAsync(UpdateTelegramPreferencesCommand command, CancellationToken ct = default);
    Task UnlinkAsync(CancellationToken ct = default);

    Task HandleUpdateAsync(TelegramBotUpdate update, CancellationToken ct = default);
}
