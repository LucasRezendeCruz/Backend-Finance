using Finance.Domain.Enums;
using FluentValidation;

namespace Finance.Application.Recurrences;

public sealed class CreateRecurrenceCommandValidator : AbstractValidator<CreateRecurrenceCommand>
{
    public CreateRecurrenceCommandValidator()
    {
        RuleFor(x => x.Type)
            .IsInEnum().WithMessage("Tipo de recorrência inválido.");

        RuleFor(x => x.Frequency)
            .IsInEnum().WithMessage("Frequência inválida.");

        RuleFor(x => x)
            .Must(x => x.AccountId.HasValue ^ x.CreditCardId.HasValue)
            .WithMessage("Informe exatamente uma conta ou um cartão de crédito.");

        RuleFor(x => x)
            .Must(x => !x.CreditCardId.HasValue || x.Type == RecurrenceType.Expense)
            .WithMessage("Recorrências no cartão de crédito devem ser do tipo EXPENSE.");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("A descrição da recorrência é obrigatória.")
            .MaximumLength(200).WithMessage("A descrição deve ter no máximo 200 caracteres.");

        RuleFor(x => x.Amount)
            .GreaterThan(0m).WithMessage("O valor da recorrência deve ser maior que zero.");

        RuleFor(x => x.CategoryId)
            .NotEmpty().WithMessage("A categoria é obrigatória.");

        RuleFor(x => x.TotalInstallments)
            .InclusiveBetween(1, 120).WithMessage("O total de parcelas deve estar entre 1 e 120.")
            .When(x => x.TotalInstallments.HasValue);
    }
}

public sealed class MaterializeRecurrencesCommandValidator : AbstractValidator<MaterializeRecurrencesCommand>
{
    public MaterializeRecurrencesCommandValidator()
    {
        RuleFor(x => x.Year)
            .InclusiveBetween(1900, 9999).WithMessage("Ano inválido.");

        RuleFor(x => x.Month)
            .InclusiveBetween(1, 12).WithMessage("Mês inválido.");
    }
}
