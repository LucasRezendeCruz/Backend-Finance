using Finance.Application.Common.Interfaces;
using Finance.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Finance.Infrastructure.Persistence.Repositories;

internal sealed class AuthRepository(FinanceDbContext dbContext) : IAuthRepository
{
    public Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken ct = default)
        => dbContext.Users.AnyAsync(u => u.Email == normalizedEmail, ct);

    public Task<User?> GetUserByEmailAsync(string normalizedEmail, CancellationToken ct = default)
        => dbContext.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail, ct);

    public Task<User?> GetUserByIdAsync(Guid userId, CancellationToken ct = default)
        => dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);

    public async Task AddUserWithDefaultCategoriesAsync(
        User user,
        IReadOnlyCollection<Category> categories,
        CancellationToken ct = default)
    {
        await dbContext.Users.AddAsync(user, ct);
        if (categories.Count > 0)
        {
            await dbContext.Categories.AddRangeAsync(categories, ct);
        }
    }

    public Task<RefreshToken?> GetRefreshTokenByHashAsync(string tokenHash, CancellationToken ct = default)
        => dbContext.RefreshTokens.FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash, ct);

    public async Task<IReadOnlyList<RefreshToken>> GetRefreshTokensByFamilyAsync(
        Guid userId,
        Guid familyId,
        CancellationToken ct = default)
        => await dbContext.RefreshTokens
            .Where(rt => rt.UserId == userId && rt.FamilyId == familyId)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<RefreshToken>> GetActiveSessionsAsync(
        Guid userId,
        DateTimeOffset utcNow,
        CancellationToken ct = default)
        => await dbContext.RefreshTokens
            .AsNoTracking()
            .Where(rt => rt.UserId == userId && rt.RevokedAtUtc == null && rt.ExpiresAtUtc > utcNow)
            .OrderByDescending(rt => rt.CreatedAtUtc)
            .ToListAsync(ct);

    public Task<RefreshToken?> GetUserRefreshTokenByIdAsync(
        Guid userId,
        Guid tokenId,
        CancellationToken ct = default)
        => dbContext.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.UserId == userId && rt.Id == tokenId, ct);

    public async Task AddRefreshTokenAsync(RefreshToken refreshToken, CancellationToken ct = default)
        => await dbContext.RefreshTokens.AddAsync(refreshToken, ct);

    public async Task AddAuditLogAsync(SecurityAuditLog auditLog, CancellationToken ct = default)
        => await dbContext.SecurityAuditLogs.AddAsync(auditLog, ct);
}
