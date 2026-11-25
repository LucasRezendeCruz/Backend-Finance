using Finance.Domain.Entities;
using Finance.Domain.Enums;

namespace Finance.Application.Common.Interfaces;

public interface ICategoryRepository
{
    Task<IReadOnlyList<Category>> GetByUserAsync(
        Guid userId,
        CategoryType? type,
        bool includeArchived,
        CancellationToken ct = default);

    Task<Category?> GetByIdAsync(Guid userId, Guid categoryId, CancellationToken ct = default);

    Task<bool> ExistsDuplicateNameAsync(
        Guid userId,
        string name,
        CategoryType type,
        Guid? excludeCategoryId = null,
        CancellationToken ct = default);

    Task AddAsync(Category category, CancellationToken ct = default);
}
