using FluentValidation;
using PersonalFinance.Application.DTOs.Auth;

namespace PersonalFinance.Application.Validators.Auth;

/// <summary>Valida o DTO de e-mail (forgot / resend).</summary>
public sealed class EmailRequestDtoValidator : AbstractValidator<EmailRequestDto>
{
    public EmailRequestDtoValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("O e-mail é obrigatório.")
            .MaximumLength(200).WithMessage("O e-mail deve ter no máximo 200 caracteres.")
            .EmailAddress().WithMessage("O e-mail informado é inválido.");
    }
}
