using Finance.Domain.Common;
using Finance.Domain.Enums;

namespace Finance.Domain.Entities;

public sealed class RefreshToken : Entity
{
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public Guid FamilyId { get; private set; }
    public string? ReplacedByTokenHash { get; private set; }
    public ClientType ClientType { get; private set; }
    public string DeviceName { get; private set; } = string.Empty;
    public string IpAddress { get; private set; } = string.Empty;
    public string UserAgent { get; private set; } = string.Empty;
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public DateTimeOffset? RevokedAtUtc { get; private set; }
    public string? RevokedReason { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    private RefreshToken() { }

    public RefreshToken(
        Guid userId,
        string tokenHash,
        Guid familyId,
        ClientType clientType,
        string deviceName,
        string ipAddress,
        string userAgent,
        DateTimeOffset expiresAtUtc,
        DateTimeOffset createdAtUtc)
    {
        if (userId == Guid.Empty)
            throw new DomainException("invalid_user_id", "Usuário inválido.");
        if (string.IsNullOrWhiteSpace(tokenHash))
            throw new DomainException("invalid_token_hash", "O hash do refresh token é obrigatório.");
        if (familyId == Guid.Empty)
            throw new DomainException("invalid_family_id", "A família da sessão é obrigatória.");
        if (expiresAtUtc <= createdAtUtc)
            throw new DomainException("invalid_token_expiry", "A expiração deve ser futura.");

        UserId = userId;
        TokenHash = tokenHash;
        FamilyId = familyId;
        ClientType = clientType;
        DeviceName = string.IsNullOrWhiteSpace(deviceName) ? clientType.ToString() : deviceName.Trim();
        IpAddress = ipAddress?.Trim() ?? string.Empty;
        UserAgent = userAgent?.Trim() ?? string.Empty;
        ExpiresAtUtc = expiresAtUtc;
        CreatedAtUtc = createdAtUtc;
    }

    public bool IsRevoked => RevokedAtUtc.HasValue;

    public bool IsExpired(DateTimeOffset utcNow) => utcNow >= ExpiresAtUtc;

    public bool IsActive(DateTimeOffset utcNow) => !IsRevoked && !IsExpired(utcNow);

    public void Revoke(DateTimeOffset utcNow, string reason, string? replacedByTokenHash = null)
    {
        if (IsRevoked)
            return;

        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainException("invalid_revocation_reason", "O motivo da revogação é obrigatório.");

        RevokedAtUtc = utcNow;
        RevokedReason = reason.Trim();
        ReplacedByTokenHash = replacedByTokenHash;
    }
}
