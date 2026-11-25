using Finance.Domain.Entities;
using Finance.Domain.Enums;

namespace Finance.Domain.Tests;

public sealed class RefreshTokenTests
{
    [Fact]
    public void IsActive_ValidUnrevokedTokenBeforeExpiry_ReturnsTrue()
    {
        var now = new DateTimeOffset(2026, 11, 23, 18, 0, 0, TimeSpan.Zero);
        var token = new RefreshToken(
            Guid.NewGuid(),
            "sha256_hash_1",
            Guid.NewGuid(),
            ClientType.Web,
            "Chrome Windows",
            "187.44.102.19",
            "Mozilla/5.0",
            now.AddDays(7),
            now);

        Assert.True(token.IsActive(now.AddDays(1)));
        Assert.False(token.IsExpired(now.AddDays(1)));
        Assert.False(token.IsRevoked);
    }

    [Fact]
    public void IsActive_AtOrAfterExpiration_ReturnsFalse()
    {
        var now = new DateTimeOffset(2026, 11, 23, 18, 0, 0, TimeSpan.Zero);
        var token = new RefreshToken(
            Guid.NewGuid(),
            "sha256_hash_1",
            Guid.NewGuid(),
            ClientType.Mobile,
            "iPhone 16",
            "177.92.14.88",
            "Expo",
            now.AddDays(30),
            now);

        Assert.True(token.IsExpired(now.AddDays(30)));
        Assert.False(token.IsActive(now.AddDays(30)));
    }

    [Fact]
    public void Revoke_SetsReasonAndReplacementHash_AndPreservesFirstRevocation()
    {
        var now = new DateTimeOffset(2026, 11, 23, 18, 0, 0, TimeSpan.Zero);
        var token = new RefreshToken(
            Guid.NewGuid(),
            "sha256_hash_1",
            Guid.NewGuid(),
            ClientType.Web,
            "Chrome",
            "187.44.102.19",
            "Mozilla/5.0",
            now.AddDays(7),
            now);

        token.Revoke(now.AddMinutes(5), "ROTATED", "sha256_hash_2");
        token.Revoke(now.AddMinutes(6), "LOGOUT", null);

        Assert.True(token.IsRevoked);
        Assert.False(token.IsActive(now.AddMinutes(6)));
        Assert.Equal("ROTATED", token.RevokedReason);
        Assert.Equal("sha256_hash_2", token.ReplacedByTokenHash);
    }
}
