using FluentValidation;

namespace Finance.Application.Accounts;

public sealed class CreateAccountCommandValidator : AbstractValidator<CreateAccountCommand>
{
    public CreateAccountCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("O nome da conta é obrigatório.")
            .MaximumLength(120).WithMessage("O nome da conta deve ter no máximo 120 caracteres.");

        RuleFor(x => x.Type)
            .IsInEnum().WithMessage("Tipo de conta inválido.");

        RuleFor(x => x.Color)
            .MaximumLength(20).WithMessage("A cor deve ter no máximo 20 caracteres.")
            .When(x => !string.IsNullOrWhiteSpace(x.Color));
    }
}

public sealed class UpdateAccountCommandValidator : AbstractValidator<UpdateAccountCommand>
{
    public UpdateAccountCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("O nome da conta é obrigatório.")
            .MaximumLength(120).WithMessage("O nome da conta deve ter no máximo 120 caracteres.");

        RuleFor(x => x.Type)
            .IsInEnum().WithMessage("Tipo de conta inválido.");

        RuleFor(x => x.Color)
            .MaximumLength(20).WithMessage("A cor deve ter no máximo 20 caracteres.")
            .When(x => !string.IsNullOrWhiteSpace(x.Color));
    }
}
