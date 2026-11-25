using Finance.Domain.Common;

namespace Finance.Domain.Entities;

public sealed class User : Entity
{
    public string Name { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public bool TwoFactorEnabled { get; private set; }
    public string? TwoFactorSecret { get; private set; }
    public int FailedLoginAttempts { get; private set; }
    public DateTimeOffset? LockoutEndUtc { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    private User() { }

    public User(string name, string email, string passwordHash, DateTimeOffset createdAtUtc)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("invalid_user_name", "O nome do usuário é obrigatório.");

        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new DomainException("invalid_password_hash", "O hash da senha é obrigatório.");

        Name = name.Trim();
        Email = NormalizeEmail(email);
        PasswordHash = passwordHash;
        TwoFactorEnabled = false;
        FailedLoginAttempts = 0;
        CreatedAtUtc = createdAtUtc;
    }

    public static string NormalizeEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new DomainException("invalid_email", "O e-mail é obrigatório.");

        return email.Trim().ToLowerInvariant();
    }

    public bool IsLockedOut(DateTimeOffset utcNow)
        => LockoutEndUtc.HasValue && LockoutEndUtc.Value > utcNow;

    public bool RecordFailedLogin(DateTimeOffset utcNow, int maxAttempts = 5, int lockoutMinutes = 15)
    {
        if (maxAttempts <= 0)
            throw new DomainException("invalid_lockout_policy", "O máximo de tentativas deve ser positivo.");

        FailedLoginAttempts++;

        if (FailedLoginAttempts >= maxAttempts)
        {
            LockoutEndUtc = utcNow.AddMinutes(lockoutMinutes);
            return true;
        }

        return false;
    }

    public void ResetFailedLogins()
    {
        FailedLoginAttempts = 0;
        LockoutEndUtc = null;
    }

    public void UpdatePasswordHash(string newPasswordHash)
    {
        if (string.IsNullOrWhiteSpace(newPasswordHash))
            throw new DomainException("invalid_password_hash", "O hash da senha é obrigatório.");

        PasswordHash = newPasswordHash;
    }
}
