using FluentValidation;
using PersonalFinance.Application.DTOs.Admin;

namespace PersonalFinance.Application.Validators.Config;

/// <summary>Valida a atualização de tipo de fonte (limites alinhados às colunas do banco).</summary>
public sealed class UpdateSourceTypeDtoValidator : AbstractValidator<UpdateSourceTypeDto>
{
    public UpdateSourceTypeDtoValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0).WithMessage("O identificador é obrigatório.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("O nome é obrigatório.")
            .MaximumLength(50).WithMessage("O nome deve ter no máximo 50 caracteres.");
    }
}
