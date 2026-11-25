using Finance.Domain.Enums;
using FluentValidation;

namespace Finance.Application.Transactions;

public sealed class CreateTransactionCommandValidator : AbstractValidator<CreateTransactionCommand>
{
    public CreateTransactionCommandValidator()
    {
        RuleFor(x => x)
            .Must(x => x.AccountId.HasValue ^ x.CreditCardId.HasValue)
            .WithMessage("Informe exatamente uma conta ou um cartão de crédito.");

        RuleFor(x => x.Type)
            .Must(t => t == TransactionType.Income || t == TransactionType.Expense)
            .WithMessage("O tipo do lançamento deve ser INCOME ou EXPENSE.");

        RuleFor(x => x)
            .Must(x => !x.CreditCardId.HasValue || x.Type == TransactionType.Expense)
            .WithMessage("Compras no cartão de crédito devem ser do tipo EXPENSE.");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("A descrição do lançamento é obrigatória.")
            .MaximumLength(250).WithMessage("A descrição deve ter no máximo 250 caracteres.");

        RuleFor(x => x.Amount)
            .GreaterThan(0m).WithMessage("O valor deve ser maior que zero.");

        RuleFor(x => x.Installments)
            .InclusiveBetween(1, 120).WithMessage("O número de parcelas deve estar entre 1 e 120.");

        RuleFor(x => x.Notes)
            .MaximumLength(1000).WithMessage("As observações devem ter no máximo 1000 caracteres.")
            .When(x => !string.IsNullOrWhiteSpace(x.Notes));

        RuleFor(x => x.Status)
            .IsInEnum().WithMessage("Status do lançamento inválido.")
            .When(x => x.Status.HasValue);
    }
}

public sealed class CreateTransferCommandValidator : AbstractValidator<CreateTransferCommand>
{
    public CreateTransferCommandValidator()
    {
        RuleFor(x => x.SourceAccountId)
            .NotEmpty().WithMessage("A conta de origem é obrigatória.");

        RuleFor(x => x.DestinationAccountId)
            .NotEmpty().WithMessage("A conta de destino é obrigatória.")
            .NotEqual(x => x.SourceAccountId)
            .WithMessage("A conta de destino deve ser diferente da conta de origem.");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("A descrição da transferência é obrigatória.")
            .MaximumLength(250).WithMessage("A descrição deve ter no máximo 250 caracteres.");

        RuleFor(x => x.Amount)
            .GreaterThan(0m).WithMessage("O valor da transferência deve ser maior que zero.");

        RuleFor(x => x.Notes)
            .MaximumLength(1000).WithMessage("As observações devem ter no máximo 1000 caracteres.")
            .When(x => !string.IsNullOrWhiteSpace(x.Notes));
    }
}

public sealed class UpdateTransactionCommandValidator : AbstractValidator<UpdateTransactionCommand>
{
    public UpdateTransactionCommandValidator()
    {
        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("A descrição do lançamento é obrigatória.")
            .MaximumLength(250).WithMessage("A descrição deve ter no máximo 250 caracteres.");

        RuleFor(x => x.Amount)
            .GreaterThan(0m).WithMessage("O valor deve ser maior que zero.");

        RuleFor(x => x.Notes)
            .MaximumLength(1000).WithMessage("As observações devem ter no máximo 1000 caracteres.")
            .When(x => !string.IsNullOrWhiteSpace(x.Notes));
    }
}

public sealed class AnticipateInstallmentsCommandValidator : AbstractValidator<AnticipateInstallmentsCommand>
{
    public AnticipateInstallmentsCommandValidator()
    {
        RuleFor(x => x)
            .Must(x =>
            {
                int modes = (x.TransactionIds is { Count: > 0 } ? 1 : 0)
                    + (x.InstallmentNumbers is { Count: > 0 } ? 1 : 0)
                    + (x.Count.HasValue ? 1 : 0);
                return modes == 1;
            })
            .WithMessage("Informe exatamente uma forma de seleção: transactionIds, installmentNumbers ou count.");

        RuleFor(x => x.Count)
            .InclusiveBetween(1, 120).WithMessage("A quantidade de parcelas a antecipar deve estar entre 1 e 120.")
            .When(x => x.Count.HasValue);

        RuleFor(x => x.TransactionIds)
            .Must(ids => ids is not null && ids.All(id => id != Guid.Empty) && ids.Distinct().Count() == ids.Count)
            .WithMessage("Os identificadores das parcelas devem ser válidos e sem duplicidades.")
            .When(x => x.TransactionIds is { Count: > 0 });

        RuleFor(x => x.InstallmentNumbers)
            .Must(nums => nums is not null && nums.All(n => n >= 1) && nums.Distinct().Count() == nums.Count)
            .WithMessage("Os números das parcelas devem ser positivos e sem duplicidades.")
            .When(x => x.InstallmentNumbers is { Count: > 0 });

        RuleFor(x => x.DiscountAmount)
            .GreaterThanOrEqualTo(0m).WithMessage("O desconto não pode ser negativo.");

        RuleFor(x => x.SelectionOrder)
            .IsInEnum().WithMessage("Ordem de seleção inválida.");

        RuleFor(x => x.Mode)
            .IsInEnum().WithMessage("Modo de antecipação inválido.")
            .When(x => x.Mode.HasValue);

        RuleFor(x => x.TargetInvoiceId)
            .Must(id => id != Guid.Empty).WithMessage("Fatura de destino inválida.")
            .When(x => x.TargetInvoiceId.HasValue);

        RuleFor(x => x.PaymentAccountId)
            .Must(id => id != Guid.Empty).WithMessage("Conta pagadora inválida.")
            .When(x => x.PaymentAccountId.HasValue);
    }
}
