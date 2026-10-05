using FluentValidation;
using PersonalFinance.Application.DTOs.Financial;

namespace PersonalFinance.Application.Validators.Financial;

/// <summary>Valida a criação de despesas em lote. Teto de 500 itens por requisição (proteção contra payload abusivo).</summary>
public sealed class CreateExpensesBatchDtoValidator : AbstractValidator<CreateExpensesBatchDto>
{
    private const int MaxItems = 500;

    public CreateExpensesBatchDtoValidator()
    {
        RuleFor(x => x.PeriodId)
            .NotEmpty().WithMessage("O período é obrigatório.");

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("O usuário é obrigatório.");

        RuleFor(x => x.Items)
            .NotNull().WithMessage("A lista de despesas é obrigatória.")
            .NotEmpty().WithMessage("A lista de despesas não pode ser vazia.")
            .Must(items => items is null || items.Count <= MaxItems)
            .WithMessage($"A lista de despesas deve ter no máximo {MaxItems} itens.");

        RuleForEach(x => x.Items)
            .SetValidator(new BatchExpenseItemDtoValidator());
    }
}
