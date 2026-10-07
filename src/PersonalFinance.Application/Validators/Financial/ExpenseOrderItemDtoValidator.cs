using FluentValidation;
using PersonalFinance.Application.DTOs.Financial;

namespace PersonalFinance.Application.Validators.Financial;

/// <summary>Valida cada item da ordenação de despesas.</summary>
public sealed class ExpenseOrderItemDtoValidator : AbstractValidator<ExpenseOrderItemDto>
{
    public ExpenseOrderItemDtoValidator()
    {
        RuleFor(x => x.ExpenseId)
            .NotEmpty().WithMessage("O identificador da despesa é obrigatório.");

        RuleFor(x => x.Order)
            .GreaterThanOrEqualTo(0).WithMessage("A ordem não pode ser negativa.");
    }
}
