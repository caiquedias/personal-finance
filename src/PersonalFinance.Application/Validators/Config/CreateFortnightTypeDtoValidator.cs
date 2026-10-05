using FluentValidation;
using PersonalFinance.Application.DTOs.Admin;

namespace PersonalFinance.Application.Validators.Config;

/// <summary>Valida a criação de tipo de quinzena (limites alinhados às colunas do banco).</summary>
public sealed class CreateFortnightTypeDtoValidator : AbstractValidator<CreateFortnightTypeDto>
{
    public CreateFortnightTypeDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("O nome é obrigatório.")
            .MaximumLength(50).WithMessage("O nome deve ter no máximo 50 caracteres.");
    }
}
