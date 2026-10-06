using FluentValidation;
using PersonalFinance.Application.DTOs.Auth;

namespace PersonalFinance.Application.Validators.Auth;

/// <summary>Valida a confirmação de e-mail: e-mail e código de 6 dígitos ASCII.</summary>
public sealed class ConfirmEmailDtoValidator : AbstractValidator<ConfirmEmailDto>
{
    public ConfirmEmailDtoValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("O e-mail é obrigatório.")
            .MaximumLength(200).WithMessage("O e-mail deve ter no máximo 200 caracteres.")
            .EmailAddress().WithMessage("O e-mail informado é inválido.");

        // [0-9] (e não \d) para não aceitar dígitos Unicode; \z evita o \n final aceito por $
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("O código é obrigatório.")
            .Matches(@"^[0-9]{6}\z").WithMessage("O código deve ter 6 dígitos.");
    }
}
