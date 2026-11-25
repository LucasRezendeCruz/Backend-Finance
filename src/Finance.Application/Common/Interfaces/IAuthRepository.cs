using Finance.Domain.Entities;

namespace Finance.Application.Common.Interfaces;

public interface IAuthRepository
{
    Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken ct = default);
    Task<User?> GetUserByEmailAsync(string normalizedEmail, CancellationToken ct = default);
    Task<User?> GetUserByIdAsync(Guid userId, CancellationToken ct = default);
    Task AddUserWithDefaultCategoriesAsync(User user, IReadOnlyCollection<Category> categories, CancellationToken ct = default);
    Task<RefreshToken?> GetRefreshTokenByHashAsync(string tokenHash, CancellationToken ct = default);
    Task<IReadOnlyList<RefreshToken>> GetRefreshTokensByFamilyAsync(Guid userId, Guid familyId, CancellationToken ct = default);
    Task<IReadOnlyList<RefreshToken>> GetActiveSessionsAsync(Guid userId, DateTimeOffset utcNow, CancellationToken ct = default);
    Task<RefreshToken?> GetUserRefreshTokenByIdAsync(Guid userId, Guid tokenId, CancellationToken ct = default);
    Task AddRefreshTokenAsync(RefreshToken refreshToken, CancellationToken ct = default);
    Task AddAuditLogAsync(SecurityAuditLog auditLog, CancellationToken ct = default);
}
