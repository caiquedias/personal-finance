using FluentValidation;
using PersonalFinance.Application.DTOs.Financial;

namespace PersonalFinance.Application.Validators.Financial;

/// <summary>Valida a replicação de despesas. Teto de 500 despesas por requisição.</summary>
public sealed class ReplicateExpensesDtoValidator : AbstractValidator<ReplicateExpensesDto>
{
    private const int MaxExpenses = 500;

    public ReplicateExpensesDtoValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("O usuário é obrigatório.");

        RuleFor(x => x.TargetPeriodId)
            .NotEmpty().WithMessage("O período destino é obrigatório.");

        RuleFor(x => x.ExpenseIds)
            .NotNull().WithMessage("A lista de despesas é obrigatória.")
            .NotEmpty().WithMessage("A lista de despesas não pode ser vazia.")
            .Must(ids => ids is null || ids.Count <= MaxExpenses)
            .WithMessage($"A lista de despesas deve ter no máximo {MaxExpenses} itens.");

        RuleForEach(x => x.ExpenseIds)
            .NotEmpty().WithMessage("O identificador da despesa é inválido.");
    }
}
