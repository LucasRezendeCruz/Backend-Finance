using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Finance.Application.Common.Interfaces;
using Finance.Domain.Entities;
using Finance.Domain.Enums;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Finance.Infrastructure.Security;

public sealed class JwtTokenService(
    IOptions<JwtOptions> options,
    IDateTimeProvider dateTimeProvider) : ITokenService
{
    private readonly JwtOptions _options = options.Value;
    private readonly JsonWebTokenHandler _tokenHandler = new() { SetDefaultTimesOnTokenCreation = false };

    public AccessTokenResult GenerateAccessToken(User user, Guid sessionFamilyId, ClientType clientType)
    {
        SymmetricSecurityKey signingKey = CreateValidatedSigningKey();
        DateTimeOffset utcNow = dateTimeProvider.UtcNow;
        DateTimeOffset expiresAtUtc = utcNow.AddMinutes(_options.AccessTokenMinutes);
        string jti = Guid.CreateVersion7().ToString();

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = utcNow.UtcDateTime,
            NotBefore = utcNow.UtcDateTime,
            Expires = expiresAtUtc.UtcDateTime,
            SigningCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256),
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = user.Id.ToString(),
                [JwtRegisteredClaimNames.Jti] = jti,
                ["sid"] = sessionFamilyId.ToString(),
                ["client_type"] = clientType.ToString().ToLowerInvariant()
            }
        };

        string token = _tokenHandler.CreateToken(descriptor);
        return new AccessTokenResult(token, expiresAtUtc, jti);
    }

    public RefreshTokenPair GenerateRefreshToken(ClientType clientType)
    {
        byte[] randomBytes = RandomNumberGenerator.GetBytes(32);
        string rawToken = Base64Url.EncodeToString(randomBytes);
        string tokenHash = HashRefreshToken(rawToken);

        int ttlDays = clientType == ClientType.Web
            ? _options.WebRefreshTokenDays
            : _options.MobileRefreshTokenDays;

        DateTimeOffset expiresAtUtc = dateTimeProvider.UtcNow.AddDays(ttlDays);
        return new RefreshTokenPair(rawToken, tokenHash, expiresAtUtc);
    }

    public string HashRefreshToken(string rawToken)
    {
        byte[] hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexStringLower(hashBytes);
    }

    private SymmetricSecurityKey CreateValidatedSigningKey()
    {
        if (string.IsNullOrWhiteSpace(_options.SecretKey) ||
            Encoding.UTF8.GetByteCount(_options.SecretKey) < JwtOptions.MinimumSecretKeyBytes)
        {
            throw new InvalidOperationException(
                "A chave JWT (Jwt:SecretKey) deve possuir pelo menos 256 bits (32 bytes).");
        }

        return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SecretKey));
    }
}
