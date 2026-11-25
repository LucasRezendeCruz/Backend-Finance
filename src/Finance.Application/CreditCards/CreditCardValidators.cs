using FluentValidation;

namespace Finance.Application.CreditCards;

public sealed class CreateCreditCardCommandValidator : AbstractValidator<CreateCreditCardCommand>
{
    public CreateCreditCardCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("O nome do cartão é obrigatório.")
            .MaximumLength(120).WithMessage("O nome do cartão deve ter no máximo 120 caracteres.");

        RuleFor(x => x.CreditLimit)
            .GreaterThan(0m).WithMessage("O limite do cartão deve ser maior que zero.");

        RuleFor(x => x.ClosingDay)
            .InclusiveBetween(1, 31).WithMessage("O dia de fechamento deve estar entre 1 e 31.");

        RuleFor(x => x.DueDay)
            .InclusiveBetween(1, 31).WithMessage("O dia de vencimento deve estar entre 1 e 31.");

        RuleFor(x => x.Color)
            .MaximumLength(20).WithMessage("A cor deve ter no máximo 20 caracteres.")
            .When(x => !string.IsNullOrWhiteSpace(x.Color));
    }
}

public sealed class UpdateCreditCardCommandValidator : AbstractValidator<UpdateCreditCardCommand>
{
    public UpdateCreditCardCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("O nome do cartão é obrigatório.")
            .MaximumLength(120).WithMessage("O nome do cartão deve ter no máximo 120 caracteres.");

        RuleFor(x => x.CreditLimit)
            .GreaterThan(0m).WithMessage("O limite do cartão deve ser maior que zero.");

        RuleFor(x => x.ClosingDay)
            .InclusiveBetween(1, 31).WithMessage("O dia de fechamento deve estar entre 1 e 31.");

        RuleFor(x => x.DueDay)
            .InclusiveBetween(1, 31).WithMessage("O dia de vencimento deve estar entre 1 e 31.");

        RuleFor(x => x.Color)
            .MaximumLength(20).WithMessage("A cor deve ter no máximo 20 caracteres.")
            .When(x => !string.IsNullOrWhiteSpace(x.Color));
    }
}

public sealed class PayInvoiceCommandValidator : AbstractValidator<PayInvoiceCommand>
{
    public PayInvoiceCommandValidator()
    {
        RuleFor(x => x.AccountId)
            .Must(id => id != Guid.Empty).WithMessage("Conta pagadora inválida.")
            .When(x => x.AccountId.HasValue);
    }
}
