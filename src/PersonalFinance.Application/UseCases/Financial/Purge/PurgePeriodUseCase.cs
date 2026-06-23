using System.Text.Json;
using PersonalFinance.Domain.Entities.Financial;
using PersonalFinance.Domain.Exceptions;
using PersonalFinance.Domain.Interfaces.Repositories;

namespace PersonalFinance.Application.UseCases.Financial.Purge;

/// <summary>
/// Expurga um período: insere PurgeRecord com snapshot dos totais,
/// deleta fisicamente todas as despesas, receitas e o próprio período.
/// Tudo executado em uma única transação.
/// </summary>
public sealed class PurgePeriodUseCase
{
    private readonly IPeriodRepository  _periodRepository;
    private readonly IExpenseRepository _expenseRepository;
    private readonly IIncomeRepository  _incomeRepository;
    private readonly IPurgeRepository   _purgeRepository;
    private readonly IUnitOfWork        _unitOfWork;

    public PurgePeriodUseCase(
        IPeriodRepository  periodRepository,
        IExpenseRepository expenseRepository,
        IIncomeRepository  incomeRepository,
        IPurgeRepository   purgeRepository,
        IUnitOfWork        unitOfWork)
    {
        _periodRepository  = periodRepository;
        _expenseRepository = expenseRepository;
        _incomeRepository  = incomeRepository;
        _purgeRepository   = purgeRepository;
        _unitOfWork        = unitOfWork;
    }

    public async Task ExecuteAsync(
        Guid periodId,
        Guid userId,
        string csvFileName,
        CancellationToken ct = default)
    {
        var period = await _periodRepository.GetByIdAndUserAsync(periodId, userId, ct)
            ?? throw new DomainException(
                "Período não encontrado ou sem permissão de acesso.");

        var expenses = (await _expenseRepository.GetByPeriodAsync(periodId, userId, ct)).ToList();
        var incomes  = (await _incomeRepository.GetByPeriodAsync(periodId, userId, ct)).ToList();

        var totalExpense  = expenses.Sum(e => e.Amount);
        var totalIncome   = incomes.Sum(i => i.Amount);
        var expenseCount  = expenses.Count;
        var incomeCount   = incomes.Count;

        // Agrupa despesas por categoria e serializa como JSON
        string categorySummaryJson;
        if (expenses.Count > 0)
        {
            var dict = expenses
                .GroupBy(e => e.CategoryId)
                .ToDictionary(g => g.Key, g => g.Sum(e => e.Amount));
            categorySummaryJson = JsonSerializer.Serialize(dict);
        }
        else
        {
            categorySummaryJson = "{}";
        }

        var purgeRecord = PurgeRecord.Create(
            userId:              userId,
            periodYear:          period.Year,
            periodMonth:         period.Month,
            totalIncome:         totalIncome,
            totalExpense:        totalExpense,
            incomeCount:         incomeCount,
            expenseCount:        expenseCount,
            categorySummaryJson: categorySummaryJson,
            csvFileName:         csvFileName);

        await _purgeRepository.AddAsync(purgeRecord, ct);

        // Exclusão física das despesas
        foreach (var expense in expenses)
            await _expenseRepository.DeleteAsync(expense, ct);

        // Exclusão física das receitas
        foreach (var income in incomes)
            await _incomeRepository.DeleteAsync(income, ct);

        // Exclusão física do período
        await _periodRepository.DeleteAsync(period, ct);

        await _unitOfWork.CommitAsync(ct);
    }
}
