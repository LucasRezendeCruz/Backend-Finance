using FluentValidation;

namespace Finance.Application.Categories;

public sealed class CreateCategoryCommandValidator : AbstractValidator<CreateCategoryCommand>
{
    public CreateCategoryCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("O nome da categoria é obrigatório.")
            .MaximumLength(100).WithMessage("O nome da categoria deve ter no máximo 100 caracteres.");

        RuleFor(x => x.Type)
            .IsInEnum().WithMessage("Tipo de categoria inválido.");

        RuleFor(x => x.Icon)
            .MaximumLength(60).WithMessage("O ícone deve ter no máximo 60 caracteres.")
            .When(x => !string.IsNullOrWhiteSpace(x.Icon));

        RuleFor(x => x.Color)
            .MaximumLength(20).WithMessage("A cor deve ter no máximo 20 caracteres.")
            .When(x => !string.IsNullOrWhiteSpace(x.Color));

        RuleFor(x => x.MonthlyBudget)
            .GreaterThan(0m).WithMessage("O orçamento mensal deve ser maior que zero.")
            .When(x => x.MonthlyBudget.HasValue);
    }
}

public sealed class UpdateCategoryCommandValidator : AbstractValidator<UpdateCategoryCommand>
{
    public UpdateCategoryCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("O nome da categoria é obrigatório.")
            .MaximumLength(100).WithMessage("O nome da categoria deve ter no máximo 100 caracteres.");

        RuleFor(x => x.Icon)
            .MaximumLength(60).WithMessage("O ícone deve ter no máximo 60 caracteres.")
            .When(x => !string.IsNullOrWhiteSpace(x.Icon));

        RuleFor(x => x.Color)
            .MaximumLength(20).WithMessage("A cor deve ter no máximo 20 caracteres.")
            .When(x => !string.IsNullOrWhiteSpace(x.Color));

        RuleFor(x => x.MonthlyBudget)
            .GreaterThan(0m).WithMessage("O orçamento mensal deve ser maior que zero.")
            .When(x => x.MonthlyBudget.HasValue);
    }
}
