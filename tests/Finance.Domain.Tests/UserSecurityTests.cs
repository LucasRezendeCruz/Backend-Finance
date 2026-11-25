using Finance.Domain.Common;
using Finance.Domain.Entities;

namespace Finance.Domain.Tests;

public sealed class UserSecurityTests
{
    [Fact]
    public void Constructor_NormalizesEmailToLowercaseAndTrimmed()
    {
        var user = new User("Lucas", "  Lucas.Dev@Exemplo.COM  ", "argon2id_hash", DateTimeOffset.UtcNow);

        Assert.Equal("lucas.dev@exemplo.com", user.Email);
        Assert.False(user.TwoFactorEnabled);
    }

    [Fact]
    public void RecordFailedLogin_LocksAccountOnFifthAttemptFor15Minutes()
    {
        var now = new DateTimeOffset(2026, 11, 23, 18, 0, 0, TimeSpan.Zero);
        var user = new User("Lucas", "lucas@exemplo.com", "argon2id_hash", now);

        for (int i = 0; i < 4; i++)
        {
            bool locked = user.RecordFailedLogin(now.AddSeconds(i));
            Assert.False(locked);
            Assert.False(user.IsLockedOut(now.AddSeconds(i)));
        }

        bool lockedOnFifth = user.RecordFailedLogin(now.AddSeconds(10));

        Assert.True(lockedOnFifth);
        Assert.True(user.IsLockedOut(now.AddMinutes(10)));
        Assert.False(user.IsLockedOut(now.AddMinutes(16)));
    }

    [Fact]
    public void ResetFailedLogins_ClearsLockoutAndAttempts()
    {
        var now = new DateTimeOffset(2026, 11, 23, 18, 0, 0, TimeSpan.Zero);
        var user = new User("Lucas", "lucas@exemplo.com", "argon2id_hash", now);

        for (int i = 0; i < 5; i++)
            user.RecordFailedLogin(now);

        Assert.True(user.IsLockedOut(now));

        user.ResetFailedLogins();

        Assert.Equal(0, user.FailedLoginAttempts);
        Assert.False(user.IsLockedOut(now));
    }

    [Fact]
    public void Constructor_BlankEmail_ThrowsDomainException()
    {
        Assert.Throws<DomainException>(() => new User("Lucas", "   ", "hash", DateTimeOffset.UtcNow));
    }
}
