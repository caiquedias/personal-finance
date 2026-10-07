using FluentValidation;
using PersonalFinance.Application.DTOs.Financial;

namespace PersonalFinance.Application.Validators.Financial;

/// <summary>Valida a criação de período (UserId vem do token, mesclado antes do use case).</summary>
public sealed class CreatePeriodDtoValidator : AbstractValidator<CreatePeriodDto>
{
    public CreatePeriodDtoValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("O usuário é obrigatório.");

        RuleFor(x => x.Year)
            .InclusiveBetween(1900, 2100).WithMessage("O ano deve estar entre 1900 e 2100.");

        RuleFor(x => x.Month)
            .InclusiveBetween(1, 12).WithMessage("O mês deve estar entre 1 e 12.");
    }
}
