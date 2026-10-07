using FluentValidation;
using PersonalFinance.Application.DTOs.Auth;

namespace PersonalFinance.Application.Validators.Auth;

/// <summary>Valida a conclusão do reset: e-mail, código de 6 dígitos ASCII e política de senha do cadastro.</summary>
public sealed class CompletePasswordResetDtoValidator : AbstractValidator<CompletePasswordResetDto>
{
    public CompletePasswordResetDtoValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("O e-mail é obrigatório.")
            .MaximumLength(200).WithMessage("O e-mail deve ter no máximo 200 caracteres.")
            .EmailAddress().WithMessage("O e-mail informado é inválido.");

        // [0-9] (e não \d) para não aceitar dígitos Unicode; \z evita o \n final aceito por $
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("O código é obrigatório.")
            .Matches(@"^[0-9]{6}\z").WithMessage("O código deve ter 6 dígitos.");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("A senha é obrigatória.")
            .MinimumLength(8).WithMessage("A senha deve ter no mínimo 8 caracteres.")
            .MaximumLength(128).WithMessage("A senha deve ter no máximo 128 caracteres.");
    }
}
