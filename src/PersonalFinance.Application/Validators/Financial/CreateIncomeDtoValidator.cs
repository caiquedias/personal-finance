using FluentValidation;
using PersonalFinance.Application.DTOs.Financial;

namespace PersonalFinance.Application.Validators.Financial;

/// <summary>Valida a criação de receita (UserId mesclado pelo controller).</summary>
public sealed class CreateIncomeDtoValidator : AbstractValidator<CreateIncomeDto>
{
    public CreateIncomeDtoValidator()
    {
        RuleFor(x => x.PeriodId)
            .NotEmpty().WithMessage("O período é obrigatório.");

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("O usuário é obrigatório.");

        RuleFor(x => x.FortnightType)
            .IsInEnum().WithMessage("Quinzena inválida.");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("A descrição é obrigatória.")
            .MaximumLength(200).WithMessage("A descrição deve ter no máximo 200 caracteres.");

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("O valor deve ser maior que zero.")
            .LessThanOrEqualTo(999_999_999.99m).WithMessage("O valor excede o limite permitido.")
            .PrecisionScale(11, 2, true).WithMessage("O valor deve ter no máximo 2 casas decimais.");

        RuleFor(x => x.ReceivedAt)
            .NotEqual(default(DateOnly)).WithMessage("A data de recebimento é obrigatória.");

        RuleFor(x => x.Notes)
            .MaximumLength(500).WithMessage("As observações devem ter no máximo 500 caracteres.");
    }
}
