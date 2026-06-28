using PersonalFinance.Application.DTOs.Financial;
using PersonalFinance.Domain.Interfaces.Repositories;

namespace PersonalFinance.Application.UseCases.Financial.Purge;

/// <summary>
/// Retorna os períodos elegíveis ao expurgo do usuário (IsActive = false),
/// com totais de receitas, despesas e contagem de itens por período.
/// </summary>
public sealed class GetEligiblePeriodsUseCase
{
    private readonly IPeriodRepository  _periodRepository;
    private readonly IExpenseRepository _expenseRepository;
    private readonly IIncomeRepository  _incomeRepository;

    public GetEligiblePeriodsUseCase(
        IPeriodRepository  periodRepository,
        IExpenseRepository expenseRepository,
        IIncomeRepository  incomeRepository)
    {
        _periodRepository  = periodRepository;
        _expenseRepository = expenseRepository;
        _incomeRepository  = incomeRepository;
    }

    public async Task<IEnumerable<EligiblePeriodDto>> ExecuteAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        var periods = await _periodRepository.GetByUserAsync(userId, ct);
        var eligible = periods.Where(p => !p.IsActive).ToList();

        var result = new List<EligiblePeriodDto>(eligible.Count);

        foreach (var period in eligible)
        {
            var expenses = (await _expenseRepository.GetByPeriodAsync(period.Id, userId, ct)).ToList();
            var incomes  = (await _incomeRepository.GetByPeriodAsync(period.Id, userId, ct)).ToList();

            result.Add(new EligiblePeriodDto
            {
                PeriodId     = period.Id,
                Year         = period.Year,
                Month        = period.Month,
                TotalIncome  = incomes.Sum(i => i.Amount),
                TotalExpense = expenses.Sum(e => e.Amount),
                ItemCount    = expenses.Count + incomes.Count
            });
        }

        return result;
    }
}
