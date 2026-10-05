using FluentValidation;
using PersonalFinance.Application.DTOs.Config;

namespace PersonalFinance.Application.Validators.Config;

/// <summary>Valida a criação de categoria (limites alinhados às colunas do banco).</summary>
public sealed class CreateCategoryDtoValidator : AbstractValidator<CreateCategoryDto>
{
    public CreateCategoryDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("O nome da categoria é obrigatório.")
            .MaximumLength(100).WithMessage("O nome deve ter no máximo 100 caracteres.");

        RuleFor(x => x.Color)
            .NotEmpty().WithMessage("A cor é obrigatória.")
            .Matches("^#[0-9A-Fa-f]{6}$").WithMessage("A cor deve estar no formato hexadecimal #RRGGBB.");

        RuleFor(x => x.Icon)
            .MaximumLength(50).WithMessage("O ícone deve ter no máximo 50 caracteres.");

        RuleFor(x => x.UserId)
            .Must(id => id.HasValue && id.Value != Guid.Empty).WithMessage("O usuário é obrigatório para categorias não globais.")
            .When(x => !x.IsGlobal);
    }
}
