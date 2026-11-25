using FluentValidation;

namespace Finance.Application.Auth;

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("O e-mail é obrigatório.")
            .EmailAddress().WithMessage("Informe um e-mail válido.")
            .MaximumLength(256).WithMessage("O e-mail deve ter no máximo 256 caracteres.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("A senha é obrigatória.")
            .MaximumLength(128).WithMessage("A senha deve ter no máximo 128 caracteres.");

        RuleFor(x => x.ClientType)
            .IsInEnum().WithMessage("O tipo de cliente informado é inválido.");

        RuleFor(x => x.DeviceName)
            .MaximumLength(150).WithMessage("O nome do dispositivo deve ter no máximo 150 caracteres.")
            .When(x => !string.IsNullOrWhiteSpace(x.DeviceName));
    }
}
