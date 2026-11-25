using Finance.Domain.Enums;

namespace Finance.Application.Auth;

public sealed record RegisterCommand(
    string Name,
    string Email,
    string Password,
    ClientType ClientType,
    string? DeviceName);

public sealed record LoginCommand(
    string Email,
    string Password,
    ClientType ClientType,
    string? DeviceName);

public sealed record RefreshCommand(
    string RawRefreshToken,
    ClientType ClientType,
    string? DeviceName);

public sealed record LogoutCommand(
    string? RawRefreshToken,
    ClientType? ClientType);

public sealed record AuthenticatedUserDto(
    Guid Id,
    string Name,
    string Email,
    bool TwoFactorEnabled);

public sealed record AuthSessionResult(
    AuthenticatedUserDto User,
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAtUtc,
    ClientType ClientType);

public sealed record SessionDto(
    Guid Id,
    Guid FamilyId,
    string ClientType,
    string DeviceName,
    string IpAddress,
    string UserAgent,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    bool IsCurrent);

public sealed record RevokeSessionResult(bool RevokedCurrentSession);
