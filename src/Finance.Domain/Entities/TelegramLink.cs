using Finance.Domain.Common;

namespace Finance.Domain.Entities;

public sealed class TelegramLink : Entity
{
    public Guid UserId { get; private set; }
    public long TelegramChatId { get; private set; }
    public string? TelegramUsername { get; private set; }
    public string? TelegramFirstName { get; private set; }
    public Guid? DefaultAccountId { get; private set; }
    public Guid? DefaultCreditCardId { get; private set; }
    public Guid? LastBotTransactionId { get; private set; }
    public DateTimeOffset LinkedAtUtc { get; private set; }
    public DateTimeOffset? LastInteractionAtUtc { get; private set; }

    private TelegramLink() { }

    public TelegramLink(
        Guid userId,
        long telegramChatId,
        string? telegramUsername,
        string? telegramFirstName,
        Guid? defaultAccountId,
        Guid? defaultCreditCardId,
        DateTimeOffset linkedAtUtc)
    {
        if (userId == Guid.Empty)
            throw new DomainException("invalid_user_id", "Usuário inválido.");
        if (telegramChatId == 0)
            throw new DomainException("invalid_telegram_chat_id", "Identificador do chat do Telegram inválido.");

        UserId = userId;
        TelegramChatId = telegramChatId;
        TelegramUsername = NormalizeOptional(telegramUsername, 100);
        TelegramFirstName = NormalizeOptional(telegramFirstName, 100);
        DefaultAccountId = defaultAccountId;
        DefaultCreditCardId = defaultCreditCardId;
        LinkedAtUtc = linkedAtUtc;
        LastInteractionAtUtc = linkedAtUtc;
    }

    public void UpdatePreferences(Guid? defaultAccountId, Guid? defaultCreditCardId)
    {
        DefaultAccountId = defaultAccountId;
        DefaultCreditCardId = defaultCreditCardId;
    }

    public void RecordInteraction(string? username, string? firstName, DateTimeOffset utcNow)
    {
        if (!string.IsNullOrWhiteSpace(username))
            TelegramUsername = NormalizeOptional(username, 100);
        if (!string.IsNullOrWhiteSpace(firstName))
            TelegramFirstName = NormalizeOptional(firstName, 100);
        LastInteractionAtUtc = utcNow;
    }

    public void SetLastBotTransaction(Guid? transactionId)
    {
        LastBotTransactionId = transactionId;
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        string trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
