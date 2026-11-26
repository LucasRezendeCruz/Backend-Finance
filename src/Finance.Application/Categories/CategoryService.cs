using Finance.Application.Common;
using Finance.Application.Common.Exceptions;
using Finance.Application.Common.Interfaces;
using Finance.Domain.Common;
using Finance.Domain.Entities;
using Finance.Domain.Enums;
using FluentValidation;

namespace Finance.Application.Categories;

public sealed class CategoryService(
    ICategoryRepository categoryRepository,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUserService,
    IValidator<CreateCategoryCommand> createValidator,
    IValidator<UpdateCategoryCommand> updateValidator) : ICategoryService
{
    public async Task<IReadOnlyList<CategoryDto>> GetAllAsync(
        CategoryType? type,
        bool includeArchived,
        CancellationToken ct = default)
    {
        Guid userId = ApplicationContextHelper.RequireUserId(currentUserService);
        IReadOnlyList<Category> categories = await categoryRepository.GetByUserAsync(userId, type, includeArchived, ct);
        return categories.Select(MapDto).ToArray();
    }

    public async Task<CategoryDto> CreateAsync(CreateCategoryCommand command, CancellationToken ct = default)
    {
        await createValidator.ValidateAndThrowAsync(command, ct);

        Guid userId = ApplicationContextHelper.RequireUserId(currentUserService);
        string trimmedName = command.Name.Trim();

        if (await categoryRepository.ExistsDuplicateNameAsync(userId, trimmedName, command.Type, null, ct))
        {
            throw AppException.Conflict(
                "category_already_exists",
                "Já existe uma categoria com este nome para o mesmo tipo.");
        }

        var category = new Category(
            userId,
            trimmedName,
            command.Type,
            command.Icon ?? "tag",
            command.Color ?? "#ff5a36",
            command.MonthlyBudget);

        await categoryRepository.AddAsync(category, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return MapDto(category);
    }

    public async Task<CategoryDto> UpdateAsync(Guid id, UpdateCategoryCommand command, CancellationToken ct = default)
    {
        await updateValidator.ValidateAndThrowAsync(command, ct);

        Guid userId = ApplicationContextHelper.RequireUserId(currentUserService);
        Category category = await categoryRepository.GetByIdAsync(userId, id, ct)
            ?? throw AppException.NotFound("category_not_found", "Categoria não encontrada.");

        string trimmedName = command.Name.Trim();
        if (await categoryRepository.ExistsDuplicateNameAsync(userId, trimmedName, category.Type, category.Id, ct))
        {
            throw AppException.Conflict(
                "category_already_exists",
                "Já existe uma categoria com este nome para o mesmo tipo.");
        }

        category.Update(
            trimmedName,
            command.Icon ?? string.Empty,
            command.Color ?? string.Empty,
            command.MonthlyBudget,
            command.UpdateBudget);

        await unitOfWork.SaveChangesAsync(ct);

        return MapDto(category);
    }

    public async Task<CategoryDto> SetMonthlyBudgetAsync(
        Guid id,
        decimal? monthlyBudget,
        CancellationToken ct = default)
    {
        Guid userId = ApplicationContextHelper.RequireUserId(currentUserService);
        Category category = await categoryRepository.GetByIdAsync(userId, id, ct)
            ?? throw AppException.NotFound("category_not_found", "Categoria não encontrada.");

        category.SetMonthlyBudget(monthlyBudget);
        await unitOfWork.SaveChangesAsync(ct);

        return MapDto(category);
    }

    public async Task<CategoryDto> SetArchivedAsync(Guid id, bool isArchived, CancellationToken ct = default)
    {
        Guid userId = ApplicationContextHelper.RequireUserId(currentUserService);
        Category category = await categoryRepository.GetByIdAsync(userId, id, ct)
            ?? throw AppException.NotFound("category_not_found", "Categoria não encontrada.");

        category.SetArchived(isArchived);
        await unitOfWork.SaveChangesAsync(ct);

        return MapDto(category);
    }

    private static CategoryDto MapDto(Category category)
        => new(
            category.Id,
            category.Name,
            category.Type.ToScreamingSnakeCase(),
            category.Icon,
            category.Color,
            category.IsArchived,
            category.MonthlyBudget);
}
