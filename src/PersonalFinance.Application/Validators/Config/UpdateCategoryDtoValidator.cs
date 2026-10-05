using FluentValidation;
using PersonalFinance.Application.DTOs.Config;

namespace PersonalFinance.Application.Validators.Config;

/// <summary>Valida a atualização de categoria (limites alinhados às colunas do banco).</summary>
public sealed class UpdateCategoryDtoValidator : AbstractValidator<UpdateCategoryDto>
{
    public UpdateCategoryDtoValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("O identificador da categoria é obrigatório.");

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("O usuário é obrigatório.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("O nome da categoria é obrigatório.")
            .MaximumLength(100).WithMessage("O nome deve ter no máximo 100 caracteres.");

        RuleFor(x => x.Color)
            .NotEmpty().WithMessage("A cor é obrigatória.")
            .Matches("^#[0-9A-Fa-f]{6}$").WithMessage("A cor deve estar no formato hexadecimal #RRGGBB.");

        RuleFor(x => x.Icon)
            .MaximumLength(50).WithMessage("O ícone deve ter no máximo 50 caracteres.");
    }
}
