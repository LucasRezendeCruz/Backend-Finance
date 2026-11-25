using Finance.Infrastructure.Security;
using Microsoft.Extensions.Options;

namespace Finance.Api.Tests;

public sealed class Argon2PasswordHasherTests
{
    [Fact]
    public void Hash_UsesOwaspDefaultsInPhcFormat_AndGeneratesUniqueSalts()
    {
        var hasher = new Argon2PasswordHasher(Options.Create(new Argon2Options()));

        string hash1 = hasher.Hash("SenhaSegura123!");
        string hash2 = hasher.Hash("SenhaSegura123!");

        Assert.StartsWith("$argon2id$v=19$m=65536,t=3,p=2$", hash1, StringComparison.Ordinal);
        Assert.Equal(5, hash1.Split('$', StringSplitOptions.RemoveEmptyEntries).Length);
        Assert.NotEqual(hash1, hash2);

        Assert.True(hasher.Verify("SenhaSegura123!", hash1));
        Assert.True(hasher.Verify("SenhaSegura123!", hash2));
        Assert.False(hasher.Verify("SenhaErrada123!", hash1));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-phc-hash")]
    [InlineData("$bcrypt$v=1$m=65536,t=3,p=2$c2FsdA==$aGFzaA==")]
    [InlineData("$argon2id$v=19$m=4,t=1,p=1$c2FsdA==$aGFzaA==")]
    [InlineData("$argon2id$v=19$m=4096,t=1,p=1$%%%$aGFzaA==")]
    public void Verify_MalformedOrUnsupportedHash_ReturnsFalseWithoutThrowing(string malformedHash)
    {
        var hasher = new Argon2PasswordHasher(Options.Create(new Argon2Options()));

        Assert.False(hasher.Verify("SenhaSegura123!", malformedHash));
    }
}
