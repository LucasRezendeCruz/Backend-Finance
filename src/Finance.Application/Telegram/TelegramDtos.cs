namespace Finance.Application.Telegram;

public sealed record TelegramLinkInfoDto(
    string? TelegramUsername,
    string? TelegramFirstName,
    Guid? DefaultAccountId,
    Guid? DefaultCreditCardId,
    DateTimeOffset LinkedAtUtc,
    DateTimeOffset? LastInteractionAtUtc);

public sealed record TelegramStatusDto(
    bool BotConfigured,
    string Mode,
    string? BotUsername,
    bool IsLinked,
    TelegramLinkInfoDto? Link);

public sealed record TelegramLinkTokenResultDto(
    string Token,
    string Command,
    string? DeepLink,
    DateTimeOffset ExpiresAtUtc);

public sealed record UpdateTelegramPreferencesCommand(
    Guid? DefaultAccountId,
    Guid? DefaultCreditCardId);
