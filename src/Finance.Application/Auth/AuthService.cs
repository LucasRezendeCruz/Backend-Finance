using System.Runtime.CompilerServices;
using System.Text.Json;
using Finance.Application.Common.Exceptions;
using Finance.Application.Common.Interfaces;
using Finance.Domain.Entities;
using Finance.Domain.Enums;
using FluentValidation;

namespace Finance.Application.Auth;

public sealed class AuthService(
    IAuthRepository authRepository,
    IUnitOfWork unitOfWork,
    IPasswordHasher passwordHasher,
    ITokenService tokenService,
    IDateTimeProvider dateTimeProvider,
    ICurrentUserService currentUserService,
    IValidator<RegisterCommand> registerValidator,
    IValidator<LoginCommand> loginValidator) : IAuthService
{
    private const int MaxFailedLoginAttempts = 5;
    private const int LockoutDurationMinutes = 15;
    private const int MaxDeviceNameLength = 150;
    private const int MaxIpLength = 64;
    private const int MaxUserAgentLength = 1024;

    private static readonly ConditionalWeakTable<IPasswordHasher, Lazy<string>> DummyHashCache = new();

    public async Task<AuthSessionResult> RegisterAsync(RegisterCommand command, CancellationToken ct = default)
    {
        await registerValidator.ValidateAndThrowAsync(command, ct);

        string normalizedEmail = User.NormalizeEmail(command.Email);
        if (await authRepository.EmailExistsAsync(normalizedEmail, ct))
        {
            throw AppException.Conflict(
                "email_already_registered",
                "Não foi possível concluir o cadastro com este e-mail.");
        }

        DateTimeOffset utcNow = dateTimeProvider.UtcNow;
        string passwordHash = passwordHasher.Hash(command.Password);
        var user = new User(command.Name, normalizedEmail, passwordHash, utcNow);
        IReadOnlyCollection<Category> defaultCategories = DefaultCategoriesCatalog.CreateForUser(user.Id);

        await authRepository.AddUserWithDefaultCategoriesAsync(user, defaultCategories, ct);

        string ip = Truncate(currentUserService.IpAddress, MaxIpLength);
        string userAgent = Truncate(currentUserService.UserAgent, MaxUserAgentLength);

        await authRepository.AddAuditLogAsync(
            new SecurityAuditLog(
                user.Id,
                SecurityAuditEventType.UserRegistered,
                ip,
                userAgent,
                utcNow,
                SerializeMetadata(new { clientType = FormatClientType(command.ClientType) })),
            ct);

        AuthSessionResult session = await IssueNewSessionAsync(
            user,
            Guid.CreateVersion7(),
            command.ClientType,
            command.DeviceName,
            ip,
            userAgent,
            utcNow,
            SecurityAuditEventType.LoginSuccess,
            ct);

        await unitOfWork.SaveChangesAsync(ct);
        return session;
    }

    public async Task<AuthSessionResult> LoginAsync(LoginCommand command, CancellationToken ct = default)
    {
        await loginValidator.ValidateAndThrowAsync(command, ct);

        DateTimeOffset utcNow = dateTimeProvider.UtcNow;
        string ip = Truncate(currentUserService.IpAddress, MaxIpLength);
        string userAgent = Truncate(currentUserService.UserAgent, MaxUserAgentLength);
        string normalizedEmail = User.NormalizeEmail(command.Email);

        User? user = await authRepository.GetUserByEmailAsync(normalizedEmail, ct);
        if (user is null)
        {
            _ = passwordHasher.Verify(command.Password, GetDummyPasswordHash());

            await authRepository.AddAuditLogAsync(
                new SecurityAuditLog(
                    null,
                    SecurityAuditEventType.LoginFailed,
                    ip,
                    userAgent,
                    utcNow,
                    SerializeMetadata(new { reason = "invalid_credentials", clientType = FormatClientType(command.ClientType) })),
                ct);

            await unitOfWork.SaveChangesAsync(ct);
            throw InvalidCredentialsException();
        }

        if (user.IsLockedOut(utcNow))
        {
            _ = passwordHasher.Verify(command.Password, GetDummyPasswordHash());

            await authRepository.AddAuditLogAsync(
                new SecurityAuditLog(
                    user.Id,
                    SecurityAuditEventType.LoginLocked,
                    ip,
                    userAgent,
                    utcNow,
                    SerializeMetadata(new { lockoutEndUtc = user.LockoutEndUtc, alreadyLocked = true })),
                ct);

            await unitOfWork.SaveChangesAsync(ct);
            throw InvalidCredentialsException();
        }

        if (!passwordHasher.Verify(command.Password, user.PasswordHash))
        {
            bool lockedNow = user.RecordFailedLogin(utcNow, MaxFailedLoginAttempts, LockoutDurationMinutes);

            await authRepository.AddAuditLogAsync(
                new SecurityAuditLog(
                    user.Id,
                    SecurityAuditEventType.LoginFailed,
                    ip,
                    userAgent,
                    utcNow,
                    SerializeMetadata(new { failedAttempts = user.FailedLoginAttempts, clientType = FormatClientType(command.ClientType) })),
                ct);

            if (lockedNow)
            {
                await authRepository.AddAuditLogAsync(
                    new SecurityAuditLog(
                        user.Id,
                        SecurityAuditEventType.LoginLocked,
                        ip,
                        userAgent,
                        utcNow,
                        SerializeMetadata(new { lockoutEndUtc = user.LockoutEndUtc, failedAttempts = user.FailedLoginAttempts })),
                    ct);
            }

            await unitOfWork.SaveChangesAsync(ct);
            throw InvalidCredentialsException();
        }

        user.ResetFailedLogins();

        AuthSessionResult session = await IssueNewSessionAsync(
            user,
            Guid.CreateVersion7(),
            command.ClientType,
            command.DeviceName,
            ip,
            userAgent,
            utcNow,
            SecurityAuditEventType.LoginSuccess,
            ct);

        await unitOfWork.SaveChangesAsync(ct);
        return session;
    }

    public async Task<AuthSessionResult> RefreshAsync(RefreshCommand command, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(command.RawRefreshToken))
        {
            throw InvalidRefreshTokenException();
        }

        DateTimeOffset utcNow = dateTimeProvider.UtcNow;
        string ip = Truncate(currentUserService.IpAddress, MaxIpLength);
        string userAgent = Truncate(currentUserService.UserAgent, MaxUserAgentLength);
        string tokenHash = tokenService.HashRefreshToken(command.RawRefreshToken.Trim());

        RefreshToken? existing = await authRepository.GetRefreshTokenByHashAsync(tokenHash, ct);
        if (existing is null)
        {
            throw InvalidRefreshTokenException();
        }

        if (existing.IsRevoked)
        {
            IReadOnlyList<RefreshToken> familyTokens =
                await authRepository.GetRefreshTokensByFamilyAsync(existing.UserId, existing.FamilyId, ct);

            int revokedCount = RevokeActiveTokens(familyTokens, utcNow, "REUSE_DETECTED");

            await authRepository.AddAuditLogAsync(
                new SecurityAuditLog(
                    existing.UserId,
                    SecurityAuditEventType.RefreshTokenReuseDetected,
                    ip,
                    userAgent,
                    utcNow,
                    SerializeMetadata(new
                    {
                        familyId = existing.FamilyId,
                        reusedTokenId = existing.Id,
                        revokedCount
                    })),
                ct);

            await unitOfWork.SaveChangesAsync(ct);

            throw AppException.Unauthorized(
                "refresh_token_reuse_detected",
                "Sessão encerrada por segurança devido a tentativa de reuso de credencial. Faça login novamente.");
        }

        if (existing.ClientType != command.ClientType)
        {
            throw InvalidRefreshTokenException();
        }

        if (existing.IsExpired(utcNow))
        {
            existing.Revoke(utcNow, "EXPIRED");
            await unitOfWork.SaveChangesAsync(ct);
            throw InvalidRefreshTokenException();
        }

        User? user = await authRepository.GetUserByIdAsync(existing.UserId, ct);
        if (user is null || user.IsLockedOut(utcNow))
        {
            existing.Revoke(utcNow, "USER_LOCKED");
            await unitOfWork.SaveChangesAsync(ct);
            throw InvalidRefreshTokenException();
        }

        RefreshTokenPair newPair = tokenService.GenerateRefreshToken(command.ClientType);
        existing.Revoke(utcNow, "ROTATED", newPair.TokenHash);

        string deviceName = string.IsNullOrWhiteSpace(command.DeviceName)
            ? existing.DeviceName
            : SanitizeDeviceName(command.DeviceName, command.ClientType);

        var rotatedToken = new RefreshToken(
            user.Id,
            newPair.TokenHash,
            existing.FamilyId,
            command.ClientType,
            deviceName,
            ip,
            userAgent,
            newPair.ExpiresAtUtc,
            utcNow);

        await authRepository.AddRefreshTokenAsync(rotatedToken, ct);

        AccessTokenResult access = tokenService.GenerateAccessToken(user, existing.FamilyId, command.ClientType);

        await authRepository.AddAuditLogAsync(
            new SecurityAuditLog(
                user.Id,
                SecurityAuditEventType.TokenRefreshed,
                ip,
                userAgent,
                utcNow,
                SerializeMetadata(new { familyId = existing.FamilyId, clientType = FormatClientType(command.ClientType) })),
            ct);

        await unitOfWork.SaveChangesAsync(ct);
        return BuildAuthSessionResult(user, access, newPair, command.ClientType);
    }

    public async Task LogoutAsync(LogoutCommand command, CancellationToken ct = default)
    {
        DateTimeOffset utcNow = dateTimeProvider.UtcNow;
        string ip = Truncate(currentUserService.IpAddress, MaxIpLength);
        string userAgent = Truncate(currentUserService.UserAgent, MaxUserAgentLength);

        if (!string.IsNullOrWhiteSpace(command.RawRefreshToken))
        {
            string tokenHash = tokenService.HashRefreshToken(command.RawRefreshToken.Trim());
            RefreshToken? token = await authRepository.GetRefreshTokenByHashAsync(tokenHash, ct);
            if (token is null ||
                (command.ClientType.HasValue && token.ClientType != command.ClientType.Value) ||
                token.IsRevoked)
            {
                return;
            }

            IReadOnlyList<RefreshToken> familyTokens =
                await authRepository.GetRefreshTokensByFamilyAsync(token.UserId, token.FamilyId, ct);

            RevokeActiveTokens(familyTokens, utcNow, "LOGOUT");

            await authRepository.AddAuditLogAsync(
                new SecurityAuditLog(
                    token.UserId,
                    SecurityAuditEventType.Logout,
                    ip,
                    userAgent,
                    utcNow,
                    SerializeMetadata(new { familyId = token.FamilyId, clientType = FormatClientType(token.ClientType) })),
                ct);

            await unitOfWork.SaveChangesAsync(ct);
            return;
        }

        if (currentUserService.UserId is Guid userId && currentUserService.SessionFamilyId is Guid familyId)
        {
            IReadOnlyList<RefreshToken> familyTokens =
                await authRepository.GetRefreshTokensByFamilyAsync(userId, familyId, ct);

            int revokedCount = RevokeActiveTokens(familyTokens, utcNow, "LOGOUT");
            if (revokedCount == 0)
            {
                return;
            }

            await authRepository.AddAuditLogAsync(
                new SecurityAuditLog(
                    userId,
                    SecurityAuditEventType.Logout,
                    ip,
                    userAgent,
                    utcNow,
                    SerializeMetadata(new { familyId, viaBearer = true })),
                ct);

            await unitOfWork.SaveChangesAsync(ct);
        }
    }

    public async Task<IReadOnlyList<SessionDto>> GetActiveSessionsAsync(CancellationToken ct = default)
    {
        Guid userId = RequireCurrentUserId();
        DateTimeOffset utcNow = dateTimeProvider.UtcNow;

        IReadOnlyList<RefreshToken> sessions = await authRepository.GetActiveSessionsAsync(userId, utcNow, ct);
        Guid? currentFamilyId = currentUserService.SessionFamilyId;

        return sessions
            .OrderByDescending(s => s.CreatedAtUtc)
            .Select(s => new SessionDto(
                s.Id,
                s.FamilyId,
                FormatClientType(s.ClientType),
                s.DeviceName,
                s.IpAddress,
                s.UserAgent,
                s.CreatedAtUtc,
                s.ExpiresAtUtc,
                currentFamilyId.HasValue && s.FamilyId == currentFamilyId.Value))
            .ToArray();
    }

    public async Task<RevokeSessionResult> RevokeSessionAsync(Guid sessionId, CancellationToken ct = default)
    {
        Guid userId = RequireCurrentUserId();
        RefreshToken? target = await authRepository.GetUserRefreshTokenByIdAsync(userId, sessionId, ct);
        if (target is null)
        {
            throw AppException.NotFound("session_not_found", "Sessão não encontrada.");
        }

        DateTimeOffset utcNow = dateTimeProvider.UtcNow;
        IReadOnlyList<RefreshToken> familyTokens =
            await authRepository.GetRefreshTokensByFamilyAsync(userId, target.FamilyId, ct);

        int revokedCount = RevokeActiveTokens(familyTokens, utcNow, "SESSION_REVOKED");

        string ip = Truncate(currentUserService.IpAddress, MaxIpLength);
        string userAgent = Truncate(currentUserService.UserAgent, MaxUserAgentLength);

        await authRepository.AddAuditLogAsync(
            new SecurityAuditLog(
                userId,
                SecurityAuditEventType.SessionRevoked,
                ip,
                userAgent,
                utcNow,
                SerializeMetadata(new
                {
                    revokedSessionId = sessionId,
                    familyId = target.FamilyId,
                    deviceName = target.DeviceName,
                    revokedCount
                })),
            ct);

        await unitOfWork.SaveChangesAsync(ct);

        bool revokedCurrent = currentUserService.SessionFamilyId.HasValue &&
            currentUserService.SessionFamilyId.Value == target.FamilyId;

        return new RevokeSessionResult(revokedCurrent);
    }

    private async Task<AuthSessionResult> IssueNewSessionAsync(
        User user,
        Guid familyId,
        ClientType clientType,
        string? rawDeviceName,
        string ip,
        string userAgent,
        DateTimeOffset utcNow,
        SecurityAuditEventType auditEventType,
        CancellationToken ct)
    {
        RefreshTokenPair refreshPair = tokenService.GenerateRefreshToken(clientType);
        string deviceName = SanitizeDeviceName(rawDeviceName, clientType);

        var refreshToken = new RefreshToken(
            user.Id,
            refreshPair.TokenHash,
            familyId,
            clientType,
            deviceName,
            ip,
            userAgent,
            refreshPair.ExpiresAtUtc,
            utcNow);

        await authRepository.AddRefreshTokenAsync(refreshToken, ct);

        AccessTokenResult accessToken = tokenService.GenerateAccessToken(user, familyId, clientType);

        await authRepository.AddAuditLogAsync(
            new SecurityAuditLog(
                user.Id,
                auditEventType,
                ip,
                userAgent,
                utcNow,
                SerializeMetadata(new { familyId, clientType = FormatClientType(clientType), deviceName })),
            ct);

        return BuildAuthSessionResult(user, accessToken, refreshPair, clientType);
    }

    private static AuthSessionResult BuildAuthSessionResult(
        User user,
        AccessTokenResult accessToken,
        RefreshTokenPair refreshPair,
        ClientType clientType)
        => new(
            new AuthenticatedUserDto(user.Id, user.Name, user.Email, user.TwoFactorEnabled),
            accessToken.Token,
            accessToken.ExpiresAtUtc,
            refreshPair.RawToken,
            refreshPair.ExpiresAtUtc,
            clientType);

    private Guid RequireCurrentUserId()
        => currentUserService.UserId
            ?? throw AppException.Unauthorized("authentication_required", "Autenticação obrigatória.");

    private string GetDummyPasswordHash()
        => DummyHashCache
            .GetValue(passwordHasher, static hasher => new Lazy<string>(() => hasher.Hash("FinanceDummyPassword#2026")))
            .Value;

    private static int RevokeActiveTokens(IReadOnlyList<RefreshToken> tokens, DateTimeOffset utcNow, string reason)
    {
        int count = 0;
        foreach (RefreshToken token in tokens)
        {
            if (!token.IsRevoked)
            {
                token.Revoke(utcNow, reason);
                count++;
            }
        }

        return count;
    }

    private static AppException InvalidCredentialsException()
        => AppException.Unauthorized("invalid_credentials", "E-mail ou senha inválidos.");

    private static AppException InvalidRefreshTokenException()
        => AppException.Unauthorized("refresh_token_invalid", "Sessão inválida ou expirada. Faça login novamente.");

    private static string FormatClientType(ClientType clientType)
        => clientType.ToString().ToLowerInvariant();

    private static string SanitizeDeviceName(string? deviceName, ClientType clientType)
    {
        string value = string.IsNullOrWhiteSpace(deviceName)
            ? (clientType == ClientType.Web ? "Navegador Web" : "App Mobile")
            : deviceName.Trim();

        return Truncate(value, MaxDeviceNameLength);
    }

    private static string Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        string trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private static string SerializeMetadata(object payload)
        => JsonSerializer.Serialize(payload);
}
