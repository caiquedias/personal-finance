using PersonalFinance.Application.DTOs.Financial;
using PersonalFinance.Domain.Interfaces.Repositories;

namespace PersonalFinance.Application.UseCases.Financial.Expenses;

/// <summary>
/// Retorna as despesas recorrentes do período mais recente do usuário,
/// excluindo o período informado (recém-criado).
/// </summary>
public sealed class GetRecurringExpensesFromLastPeriodUseCase
{
    private readonly IExpenseRepository _expenseRepository;

    public GetRecurringExpensesFromLastPeriodUseCase(IExpenseRepository expenseRepository)
        => _expenseRepository = expenseRepository;

    public async Task<IEnumerable<RecurringExpenseDto>> ExecuteAsync(
        Guid userId,
        Guid excludePeriodId,
        CancellationToken ct = default)
    {
        var expenses = await _expenseRepository
            .GetRecurringExpensesFromLastPeriodAsync(userId, excludePeriodId, ct);

        return expenses.Select(e => new RecurringExpenseDto(e.Id, e.Description, e.Notes, e.Amount));
    }
}
