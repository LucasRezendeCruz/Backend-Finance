namespace Finance.Api.Configuration;

public sealed class TelegramOptions
{
    public const string SectionName = "Telegram";

    public string BotToken { get; set; } = string.Empty;
    public string BotUsername { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
    public string Mode { get; set; } = "Polling";

    public void ApplyEnvironmentOverrides(IConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(BotToken))
            BotToken = configuration["TELEGRAM_BOT_TOKEN"] ?? string.Empty;
        if (string.IsNullOrWhiteSpace(BotUsername))
            BotUsername = configuration["TELEGRAM_BOT_USERNAME"] ?? string.Empty;
        if (string.IsNullOrWhiteSpace(WebhookSecret))
            WebhookSecret = configuration["TELEGRAM_WEBHOOK_SECRET"] ?? string.Empty;

        string? envMode = configuration["TELEGRAM_MODE"];
        if (!string.IsNullOrWhiteSpace(envMode))
            Mode = envMode;

        Mode = string.IsNullOrWhiteSpace(Mode) ? "Polling" : Mode.Trim();
    }
}
