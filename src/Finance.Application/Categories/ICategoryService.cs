using Finance.Domain.Enums;

namespace Finance.Application.Categories;

public interface ICategoryService
{
    Task<IReadOnlyList<CategoryDto>> GetAllAsync(CategoryType? type, bool includeArchived, CancellationToken ct = default);
    Task<CategoryDto> CreateAsync(CreateCategoryCommand command, CancellationToken ct = default);
    Task<CategoryDto> UpdateAsync(Guid id, UpdateCategoryCommand command, CancellationToken ct = default);
    Task<CategoryDto> SetArchivedAsync(Guid id, bool isArchived, CancellationToken ct = default);
}
