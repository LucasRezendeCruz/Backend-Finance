using Finance.Application.Common.Interfaces;
using Finance.Domain.Entities;
using Finance.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Finance.Infrastructure.Persistence.Repositories;

internal sealed class CategoryRepository(FinanceDbContext dbContext) : ICategoryRepository
{
    public async Task<IReadOnlyList<Category>> GetByUserAsync(
        Guid userId,
        CategoryType? type,
        bool includeArchived,
        CancellationToken ct = default)
    {
        IQueryable<Category> query = dbContext.Categories
            .AsNoTracking()
            .Where(c => c.UserId == userId);

        if (type.HasValue)
            query = query.Where(c => c.Type == type.Value);

        if (!includeArchived)
            query = query.Where(c => !c.IsArchived);

        return await query
            .OrderBy(c => c.IsArchived)
            .ThenBy(c => c.Type)
            .ThenBy(c => c.Name)
            .ToListAsync(ct);
    }

    public Task<Category?> GetByIdAsync(Guid userId, Guid categoryId, CancellationToken ct = default)
        => dbContext.Categories.FirstOrDefaultAsync(c => c.UserId == userId && c.Id == categoryId, ct);

    public async Task<bool> ExistsDuplicateNameAsync(
        Guid userId,
        string name,
        CategoryType type,
        Guid? excludeCategoryId = null,
        CancellationToken ct = default)
    {
        IQueryable<Category> query = dbContext.Categories
            .AsNoTracking()
            .Where(c => c.UserId == userId && c.Type == type);

        if (excludeCategoryId.HasValue)
            query = query.Where(c => c.Id != excludeCategoryId.Value);

        List<string> existingNames = await query.Select(c => c.Name).ToListAsync(ct);
        return existingNames.Any(existing => string.Equals(existing, name, StringComparison.OrdinalIgnoreCase));
    }

    public async Task AddAsync(Category category, CancellationToken ct = default)
        => await dbContext.Categories.AddAsync(category, ct);
}
