using PersonalFinance.Application.DTOs.Import;
using PersonalFinance.Domain.Entities.Financial;
using PersonalFinance.Domain.Enums;
using PersonalFinance.Domain.Exceptions;
using PersonalFinance.Domain.Interfaces.Repositories;

namespace PersonalFinance.Application.UseCases.Import;

/// <summary>
/// Persiste a lista revisada do extrato, criando/reaproveitando um Period por Ano+Mês.
/// Todos os itens são validados antes de qualquer gravação; um único commit ao final.
/// </summary>
public sealed class ConfirmStatementImportUseCase
{
    private const int MaxDescriptionLength = 200;

    private readonly ICategoryRepository _categoryRepository;
    private readonly IPeriodRepository _periodRepository;
    private readonly IExpenseRepository _expenseRepository;
    private readonly IIncomeRepository _incomeRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ConfirmStatementImportUseCase(
        ICategoryRepository categoryRepository,
        IPeriodRepository periodRepository,
        IExpenseRepository expenseRepository,
        IIncomeRepository incomeRepository,
        IUnitOfWork unitOfWork)
    {
        _categoryRepository = categoryRepository;
        _periodRepository = periodRepository;
        _expenseRepository = expenseRepository;
        _incomeRepository = incomeRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ConfirmStatementImportResultDto> ExecuteAsync(
        ConfirmStatementImportRequestDto request,
        Guid userId,
        CancellationToken ct = default)
    {
        var items = request?.Items;
        if (items is null || items.Count == 0)
            throw new DomainException("Nenhum lançamento informado para importação.");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var accessibleCategories = new HashSet<Guid>();
        var parsed = new List<(ConfirmStatementItemDto Item, bool IsExpense, SourceType Source)>();

        // Fase 1: valida tudo antes de gravar qualquer coisa
        foreach (var item in items)
        {
            if (item is null)
                throw new DomainException("Lançamento inválido.");

            var isExpense = string.Equals(item.Kind, "Expense", StringComparison.OrdinalIgnoreCase);
            var isIncome = string.Equals(item.Kind, "Income", StringComparison.OrdinalIgnoreCase);
            if (!isExpense && !isIncome)
                throw new DomainException("Tipo de lançamento inválido. Use 'Income' ou 'Expense'.");

            if (item.Amount <= 0)
                throw new DomainException("O valor do lançamento deve ser maior que zero.");

            if (string.IsNullOrWhiteSpace(item.Description))
                throw new DomainException("A descrição do lançamento é obrigatória.");

            if (item.Description.Length > MaxDescriptionLength)
                throw new DomainException($"A descrição deve ter no máximo {MaxDescriptionLength} caracteres.");

            if (item.Date > today)
                throw new DomainException("Não é permitido importar lançamentos com data futura.");

            var source = SourceType.Personal;
            if (isExpense)
            {
                if (item.CategoryId is null || item.CategoryId == Guid.Empty)
                    throw new DomainException("Toda despesa importada deve ter uma categoria.");

                if (!accessibleCategories.Contains(item.CategoryId.Value))
                {
                    if (!await _categoryRepository.IsAccessibleByUserAsync(item.CategoryId.Value, userId, ct))
                        throw new DomainException("Categoria inválida ou inacessível.");
                    accessibleCategories.Add(item.CategoryId.Value);
                }

                if (!string.IsNullOrWhiteSpace(item.SourceType) &&
                    !Enum.TryParse(item.SourceType, ignoreCase: true, out source))
                    throw new DomainException("Origem (SourceType) inválida.");
            }

            parsed.Add((item, isExpense, source));
        }

        // Fase 2: grava
        var periodCache = new Dictionary<(int Year, int Month), Guid>();
        int periodsCreated = 0, periodsReused = 0, expensesCreated = 0, incomesCreated = 0;

        foreach (var (item, isExpense, source) in parsed)
        {
            var key = (item.Date.Year, item.Date.Month);
            if (!periodCache.TryGetValue(key, out var periodId))
            {
                var period = await _periodRepository.GetByUserYearMonthAsync(userId, key.Item1, key.Item2, ct);
                if (period is null)
                {
                    period = Period.Create(userId, key.Item1, key.Item2);
                    await _periodRepository.AddAsync(period, ct);
                    periodsCreated++;
                }
                else
                {
                    if (period.IsDeleted)
                    {
                        period.Reactivate();
                        await _periodRepository.UpdateAsync(period, ct);
                    }
                    periodsReused++;
                }
                periodId = period.Id;
                periodCache[key] = periodId;
            }

            var fortnight = item.Date.Day <= 15 ? FortnightType.First : FortnightType.Second;

            if (isExpense)
            {
                var expense = Expense.Create(
                    periodId, userId, item.CategoryId!.Value,
                    source, fortnight, PaymentStatus.Paid,
                    item.Description.Trim(), item.Amount,
                    item.Date, item.Date, null);
                await _expenseRepository.AddAsync(expense, ct);
                expensesCreated++;
            }
            else
            {
                var income = Income.Create(
                    periodId, userId, fortnight,
                    item.Description.Trim(), item.Amount, item.Date, null);
                await _incomeRepository.AddAsync(income, ct);
                incomesCreated++;
            }
        }

        await _unitOfWork.CommitAsync(ct);

        return new ConfirmStatementImportResultDto(periodsCreated, periodsReused, expensesCreated, incomesCreated);
    }
}
