using Finance.Domain.Entities;
using Finance.Domain.Enums;

namespace Finance.Application.Auth;

internal static class DefaultCategoriesCatalog
{
    private static readonly (string Name, CategoryType Type, string Icon, string Color)[] Items =
    [
        ("Alimentação", CategoryType.Expense, "utensils", "#ff5a36"),
        ("Moradia", CategoryType.Expense, "home", "#f59e0b"),
        ("Transporte", CategoryType.Expense, "car", "#3b82f6"),
        ("Saúde", CategoryType.Expense, "heart-pulse", "#ef4444"),
        ("Lazer", CategoryType.Expense, "gamepad-2", "#a855f7"),
        ("Assinaturas", CategoryType.Expense, "repeat", "#ec4899"),
        ("Educação", CategoryType.Expense, "graduation-cap", "#06b6d4"),
        ("Outros", CategoryType.Expense, "tag", "#64748b"),
        ("Salário", CategoryType.Income, "wallet", "#22c55e"),
        ("Freelance", CategoryType.Income, "briefcase", "#10b981"),
        ("Investimentos", CategoryType.Income, "trending-up", "#14b8a6"),
        ("Outras Receitas", CategoryType.Income, "plus-circle", "#84cc16")
    ];

    public static IReadOnlyCollection<Category> CreateForUser(Guid userId)
        => Items
            .Select(item => new Category(userId, item.Name, item.Type, item.Icon, item.Color))
            .ToArray();
}
