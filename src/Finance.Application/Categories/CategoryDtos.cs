using Finance.Domain.Enums;

namespace Finance.Application.Categories;

public sealed record CreateCategoryCommand(
    string Name,
    CategoryType Type,
    string? Icon,
    string? Color);

public sealed record UpdateCategoryCommand(
    string Name,
    string? Icon,
    string? Color);

public sealed record CategoryDto(
    Guid Id,
    string Name,
    string Type,
    string Icon,
    string Color,
    bool IsArchived);
