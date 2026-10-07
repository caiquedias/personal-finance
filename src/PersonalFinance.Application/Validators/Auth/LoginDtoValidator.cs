using FluentValidation;
using PersonalFinance.Application.DTOs.Auth;

namespace PersonalFinance.Application.Validators.Auth;

/// <summary>
/// Valida o login. Sem checagem de formato de e-mail nem tamanho mínimo de senha: mantém o fluxo
/// genérico "Credenciais inválidas." e a contagem de tentativas do lockout/throttle.
/// </summary>
public sealed class LoginDtoValidator : AbstractValidator<LoginDto>
{
    public LoginDtoValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("O e-mail é obrigatório.")
            .MaximumLength(200).WithMessage("O e-mail deve ter no máximo 200 caracteres.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("A senha é obrigatória.")
            .MaximumLength(128).WithMessage("A senha deve ter no máximo 128 caracteres.");
    }
}
