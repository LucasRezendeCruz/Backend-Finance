using Finance.Domain.Entities;
using Finance.Domain.Enums;

namespace Finance.Application.Common.Interfaces;

public sealed record AccessTokenResult(string Token, DateTimeOffset ExpiresAtUtc, string Jti);

public sealed record RefreshTokenPair(string RawToken, string TokenHash, DateTimeOffset ExpiresAtUtc);

public interface ITokenService
{
    AccessTokenResult GenerateAccessToken(User user, Guid sessionFamilyId, ClientType clientType);
    RefreshTokenPair GenerateRefreshToken(ClientType clientType);
    string HashRefreshToken(string rawToken);
}
