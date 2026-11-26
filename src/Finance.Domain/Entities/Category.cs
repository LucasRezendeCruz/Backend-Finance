using Finance.Domain.Common;
using Finance.Domain.Enums;

namespace Finance.Domain.Entities;

public sealed class Category : Entity
{
    public Guid UserId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public CategoryType Type { get; private set; }
    public string Icon { get; private set; } = string.Empty;
    public string Color { get; private set; } = string.Empty;
    public decimal? MonthlyBudget { get; private set; }
    public bool IsArchived { get; private set; }

    private Category() { }

    public Category(
        Guid userId,
        string name,
        CategoryType type,
        string icon,
        string color,
        decimal? monthlyBudget = null)
    {
        if (userId == Guid.Empty)
            throw new DomainException("invalid_user_id", "Usuário inválido.");
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("invalid_category_name", "O nome da categoria é obrigatório.");

        UserId = userId;
        Name = name.Trim();
        Type = type;
        Icon = string.IsNullOrWhiteSpace(icon) ? "tag" : icon.Trim();
        Color = string.IsNullOrWhiteSpace(color) ? "#ff5a36" : color.Trim();
        IsArchived = false;
        SetMonthlyBudget(monthlyBudget);
    }

    public void Update(string name, string icon, string color, decimal? monthlyBudget = null, bool updateBudget = false)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("invalid_category_name", "O nome da categoria é obrigatório.");

        Name = name.Trim();
        if (!string.IsNullOrWhiteSpace(icon))
            Icon = icon.Trim();
        if (!string.IsNullOrWhiteSpace(color))
            Color = color.Trim();
        if (updateBudget)
            SetMonthlyBudget(monthlyBudget);
    }

    public void SetMonthlyBudget(decimal? monthlyBudget)
    {
        if (monthlyBudget is null)
        {
            MonthlyBudget = null;
            return;
        }

        if (Type != CategoryType.Expense)
        {
            throw new DomainException(
                "budget_only_for_expense",
                "Orçamento mensal só pode ser definido para categorias de despesa.");
        }

        if (monthlyBudget.Value <= 0m)
        {
            throw new DomainException(
                "invalid_monthly_budget",
                "O orçamento mensal deve ser maior que zero.");
        }

        MonthlyBudget = decimal.Round(monthlyBudget.Value, 2, MidpointRounding.AwayFromZero);
    }

    public void SetArchived(bool isArchived)
    {
        IsArchived = isArchived;
    }
}
