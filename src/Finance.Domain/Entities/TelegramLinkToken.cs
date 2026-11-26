using Finance.Domain.Common;

namespace Finance.Domain.Entities;

public sealed class TelegramLinkToken : Entity
{
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? ConsumedAtUtc { get; private set; }

    private TelegramLinkToken() { }

    public TelegramLinkToken(
        Guid userId,
        string tokenHash,
        DateTimeOffset expiresAtUtc,
        DateTimeOffset createdAtUtc)
    {
        if (userId == Guid.Empty)
            throw new DomainException("invalid_user_id", "Usuário inválido.");
        if (string.IsNullOrWhiteSpace(tokenHash))
            throw new DomainException("invalid_token_hash", "Hash do token inválido.");
        if (expiresAtUtc <= createdAtUtc)
            throw new DomainException("invalid_token_expiration", "Expiração do token inválida.");

        UserId = userId;
        TokenHash = tokenHash.Trim();
        ExpiresAtUtc = expiresAtUtc;
        CreatedAtUtc = createdAtUtc;
    }

    public bool IsUsable(DateTimeOffset utcNow)
        => ConsumedAtUtc is null && ExpiresAtUtc > utcNow;

    public void MarkConsumed(DateTimeOffset utcNow)
    {
        ConsumedAtUtc = utcNow;
    }
}
