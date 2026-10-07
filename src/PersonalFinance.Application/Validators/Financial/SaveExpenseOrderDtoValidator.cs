using FluentValidation;
using PersonalFinance.Application.DTOs.Financial;

namespace PersonalFinance.Application.Validators.Financial;

/// <summary>Valida a persistência da ordenação de despesas. Teto de 1000 itens por requisição.</summary>
public sealed class SaveExpenseOrderDtoValidator : AbstractValidator<SaveExpenseOrderDto>
{
    private const int MaxItems = 1000;

    public SaveExpenseOrderDtoValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("O usuário é obrigatório.");

        RuleFor(x => x.Items)
            .NotNull().WithMessage("A lista de ordenação é obrigatória.")
            .Must(items => items is null || items.Count() <= MaxItems)
            .WithMessage($"A lista de ordenação deve ter no máximo {MaxItems} itens.");

        RuleForEach(x => x.Items)
            .SetValidator(new ExpenseOrderItemDtoValidator());
    }
}
