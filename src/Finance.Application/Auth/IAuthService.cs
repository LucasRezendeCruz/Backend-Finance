namespace Finance.Application.Auth;

public interface IAuthService
{
    Task<AuthSessionResult> RegisterAsync(RegisterCommand command, CancellationToken ct = default);
    Task<AuthSessionResult> LoginAsync(LoginCommand command, CancellationToken ct = default);
    Task<AuthSessionResult> RefreshAsync(RefreshCommand command, CancellationToken ct = default);
    Task LogoutAsync(LogoutCommand command, CancellationToken ct = default);
    Task<IReadOnlyList<SessionDto>> GetActiveSessionsAsync(CancellationToken ct = default);
    Task<RevokeSessionResult> RevokeSessionAsync(Guid sessionId, CancellationToken ct = default);
}
