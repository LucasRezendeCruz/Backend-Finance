using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Finance.Api.Configuration;
using Finance.Application.Common.Interfaces;
using Microsoft.Extensions.Options;

namespace Finance.Api.Services.Telegram;

internal sealed class HttpTelegramBotClient(
    IHttpClientFactory httpClientFactory,
    IOptions<TelegramOptions> options,
    ILogger<HttpTelegramBotClient> logger) : ITelegramBotClient
{
    private readonly TelegramOptions _options = options.Value;
    private string? _cachedUsername;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.BotToken);
    public string Mode => _options.Mode;
    public string? ConfiguredBotUsername => string.IsNullOrWhiteSpace(_options.BotUsername) ? _cachedUsername : _options.BotUsername;
    public string? WebhookSecret => string.IsNullOrWhiteSpace(_options.WebhookSecret) ? null : _options.WebhookSecret;

    public async Task SendMessageAsync(
        long chatId,
        string text,
        IReadOnlyList<TelegramInlineButton>? inlineButtons = null,
        CancellationToken ct = default)
    {
        if (!IsConfigured)
            return;

        HttpClient client = httpClientFactory.CreateClient("telegram");
        object payload = inlineButtons is { Count: > 0 }
            ? new
            {
                chat_id = chatId,
                text,
                reply_markup = new
                {
                    inline_keyboard = new[]
                    {
                        inlineButtons.Select(b => new { text = b.Text, callback_data = b.CallbackData }).ToArray()
                    }
                }
            }
            : new
            {
                chat_id = chatId,
                text
            };

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            BuildMethodUrl("sendMessage"),
            payload,
            ct);

        if (!response.IsSuccessStatusCode)
        {
            string body = await response.Content.ReadAsStringAsync(ct);
            logger.LogWarning("Telegram sendMessage failed ({Status}): {Body}", (int)response.StatusCode, body);
        }
    }

    public async Task AnswerCallbackQueryAsync(
        string callbackQueryId,
        string? text = null,
        CancellationToken ct = default)
    {
        if (!IsConfigured || string.IsNullOrWhiteSpace(callbackQueryId))
            return;

        HttpClient client = httpClientFactory.CreateClient("telegram");
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            BuildMethodUrl("answerCallbackQuery"),
            new { callback_query_id = callbackQueryId, text },
            ct);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Telegram answerCallbackQuery failed ({Status})", (int)response.StatusCode);
        }
    }

    public async Task<IReadOnlyList<TelegramBotUpdate>> GetUpdatesAsync(
        long? offset,
        int timeoutSeconds,
        CancellationToken ct = default)
    {
        if (!IsConfigured)
            return [];

        HttpClient client = httpClientFactory.CreateClient("telegram");
        string query = offset.HasValue
            ? $"offset={offset.Value}&timeout={timeoutSeconds}&allowed_updates=%5B%22message%22%2C%22callback_query%22%5D"
            : $"timeout={timeoutSeconds}&allowed_updates=%5B%22message%22%2C%22callback_query%22%5D";

        using HttpResponseMessage response = await client.GetAsync(
            $"{BuildMethodUrl("getUpdates")}?{query}",
            ct);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Telegram getUpdates failed ({Status})", (int)response.StatusCode);
            return [];
        }

        TelegramApiEnvelope<List<TelegramRawUpdate>>? envelope =
            await response.Content.ReadFromJsonAsync<TelegramApiEnvelope<List<TelegramRawUpdate>>>(ct);

        if (envelope is null || !envelope.Ok || envelope.Result is null)
            return [];

        return envelope.Result.Select(MapUpdate).ToArray();
    }

    public async Task<string?> ResolveBotUsernameAsync(CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(ConfiguredBotUsername))
            return ConfiguredBotUsername;

        if (!IsConfigured)
            return null;

        HttpClient client = httpClientFactory.CreateClient("telegram");
        using HttpResponseMessage response = await client.GetAsync(BuildMethodUrl("getMe"), ct);
        if (!response.IsSuccessStatusCode)
            return null;

        TelegramApiEnvelope<TelegramRawUser>? envelope =
            await response.Content.ReadFromJsonAsync<TelegramApiEnvelope<TelegramRawUser>>(ct);

        if (envelope is { Ok: true, Result.Username: not null })
        {
            _cachedUsername = envelope.Result.Username;
            return _cachedUsername;
        }

        return null;
    }

    private string BuildMethodUrl(string method)
        => $"https://api.telegram.org/bot{_options.BotToken}/{method}";

    internal static TelegramBotUpdate MapUpdate(TelegramRawUpdate raw)
    {
        TelegramIncomingMessage? msg = raw.Message is not null
            ? new TelegramIncomingMessage(
                raw.Message.MessageId,
                raw.Message.Chat.Id,
                raw.Message.From?.Username,
                raw.Message.From?.FirstName,
                raw.Message.Text)
            : null;

        TelegramIncomingCallbackQuery? cb = raw.CallbackQuery is { Message: not null }
            ? new TelegramIncomingCallbackQuery(
                raw.CallbackQuery.Id,
                raw.CallbackQuery.Message.Chat.Id,
                raw.CallbackQuery.From?.Username,
                raw.CallbackQuery.From?.FirstName,
                raw.CallbackQuery.Data)
            : null;

        return new TelegramBotUpdate(raw.UpdateId, msg, cb);
    }

    private sealed record TelegramApiEnvelope<T>(
        [property: JsonPropertyName("ok")] bool Ok,
        [property: JsonPropertyName("result")] T? Result);
}

public sealed record TelegramRawUpdate(
    [property: JsonPropertyName("update_id")] long UpdateId,
    [property: JsonPropertyName("message")] TelegramRawMessage? Message,
    [property: JsonPropertyName("callback_query")] TelegramRawCallbackQuery? CallbackQuery);

public sealed record TelegramRawMessage(
    [property: JsonPropertyName("message_id")] long MessageId,
    [property: JsonPropertyName("chat")] TelegramRawChat Chat,
    [property: JsonPropertyName("from")] TelegramRawUser? From,
    [property: JsonPropertyName("text")] string? Text);

public sealed record TelegramRawChat(
    [property: JsonPropertyName("id")] long Id);

public sealed record TelegramRawUser(
    [property: JsonPropertyName("username")] string? Username,
    [property: JsonPropertyName("first_name")] string? FirstName);

public sealed record TelegramRawCallbackQuery(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("from")] TelegramRawUser? From,
    [property: JsonPropertyName("message")] TelegramRawMessage? Message,
    [property: JsonPropertyName("data")] string? Data);
