namespace Finance.Application.Common.Interfaces;

public sealed record TelegramInlineButton(string Text, string CallbackData);

public sealed record TelegramBotUpdate(
    long UpdateId,
    TelegramIncomingMessage? Message,
    TelegramIncomingCallbackQuery? CallbackQuery);

public sealed record TelegramIncomingMessage(
    long MessageId,
    long ChatId,
    string? Username,
    string? FirstName,
    string? Text);

public sealed record TelegramIncomingCallbackQuery(
    string CallbackQueryId,
    long ChatId,
    string? Username,
    string? FirstName,
    string? Data);

public interface ITelegramBotClient
{
    bool IsConfigured { get; }
    string Mode { get; }
    string? ConfiguredBotUsername { get; }
    string? WebhookSecret { get; }

    Task SendMessageAsync(
        long chatId,
        string text,
        IReadOnlyList<TelegramInlineButton>? inlineButtons = null,
        CancellationToken ct = default);

    Task AnswerCallbackQueryAsync(
        string callbackQueryId,
        string? text = null,
        CancellationToken ct = default);

    Task<IReadOnlyList<TelegramBotUpdate>> GetUpdatesAsync(
        long? offset,
        int timeoutSeconds,
        CancellationToken ct = default);

    Task<string?> ResolveBotUsernameAsync(CancellationToken ct = default);
}
