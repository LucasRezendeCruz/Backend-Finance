using Finance.Application.Common.Interfaces;
using Finance.Application.Telegram;

namespace Finance.Api.Services.Telegram;

internal sealed class TelegramPollingBackgroundService(
    ITelegramBotClient botClient,
    IServiceScopeFactory scopeFactory,
    IHostEnvironment environment,
    ILogger<TelegramPollingBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (environment.IsEnvironment("Testing"))
            return;

        if (!botClient.IsConfigured ||
            !string.Equals(botClient.Mode, "Polling", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        logger.LogInformation("Razo Telegram Bot polling started.");
        long? nextOffset = null;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                IReadOnlyList<TelegramBotUpdate> updates = await botClient.GetUpdatesAsync(
                    nextOffset,
                    timeoutSeconds: 20,
                    stoppingToken);

                foreach (TelegramBotUpdate update in updates)
                {
                    nextOffset = update.UpdateId + 1;
                    using IServiceScope scope = scopeFactory.CreateScope();
                    ITelegramBotService botService = scope.ServiceProvider.GetRequiredService<ITelegramBotService>();
                    await botService.HandleUpdateAsync(update, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Telegram polling error. Retrying in 5s.");
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }
}
