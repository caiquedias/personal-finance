using Microsoft.EntityFrameworkCore;
using PersonalFinance.Domain.Entities.Financial;
using PersonalFinance.Domain.Enums;
using PersonalFinance.Domain.Interfaces.Repositories;
using PersonalFinance.Infrastructure.Persistence.Context;

namespace PersonalFinance.Infrastructure.Persistence.Repositories.Financial;

public sealed class ExpenseRepository : IExpenseRepository
{
    private readonly AppDbContext _context;
    public ExpenseRepository(AppDbContext context) => _context = context;

    public async Task<Expense?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.Expenses.FirstOrDefaultAsync(e => e.Id == id, ct);

    public async Task<Expense?> GetByIdAndUserAsync(
        Guid id, Guid userId, CancellationToken ct = default)
        => await _context.Expenses
               .FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId, ct);

    public async Task<IEnumerable<Expense>> GetByPeriodAsync(
        Guid periodId, Guid userId, CancellationToken ct = default)
        => await _context.Expenses
               .Where(e => e.PeriodId == periodId && e.UserId == userId)
               .OrderBy(e => e.FortnightType)
               .ThenBy(e => e.DueDate)
               .ToListAsync(ct);

    /// <summary>
    /// Busca por chave de idempotência: PeriodId + Descrição + Valor + Quinzena.
    /// Ignora soft delete para detectar registros que foram desativados manualmente
    /// e evitar duplicatas ao re-importar.
    /// </summary>
    public async Task<Expense?> FindByImportKeyAsync(
        Guid periodId,
        string description,
        decimal amount,
        FortnightType fortnightType,
        CancellationToken ct = default)
        => await _context.Expenses
               .IgnoreQueryFilters()
               .FirstOrDefaultAsync(e =>
                   e.PeriodId == periodId &&
                   e.Description.ToLower() == description.ToLower() &&
                   e.Amount == amount &&
                   e.FortnightType == fortnightType, ct);

    public async Task AddAsync(Expense expense, CancellationToken ct = default)
        => await _context.Expenses.AddAsync(expense, ct);

    public Task UpdateAsync(Expense expense, CancellationToken ct = default)
    {
        _context.Expenses.Update(expense);
        return Task.CompletedTask;
    }

    public Task UpdateRangeAsync(IEnumerable<Expense> expenses, CancellationToken ct = default)
    {
        _context.Expenses.UpdateRange(expenses);
        return Task.CompletedTask;
    }

    public async Task<IEnumerable<Expense>> GetByIdsAndUserAsync(
        IReadOnlyList<Guid> ids,
        Guid userId,
        CancellationToken ct = default)
        => await _context.Expenses
               .Where(e => ids.Contains(e.Id) && e.UserId == userId)
               .ToListAsync(ct);

    public async Task<(IEnumerable<Expense> Items, int TotalCount)> GetPagedByPeriodAsync(
        Guid          periodId,
        Guid          userId,
        int           pageNumber,
        int           pageSize,
        string?       description,
        Guid?         categoryId,
        PaymentStatus? paymentStatus,
        FortnightType? fortnightType,
        SourceType?    sourceType,
        CancellationToken ct = default)
    {
        var baseQuery = _context.Expenses
            .Where(e => e.PeriodId == periodId && e.UserId == userId);

        if (!string.IsNullOrWhiteSpace(description))
            baseQuery = baseQuery.Where(e => e.Description.Contains(description));

        if (categoryId.HasValue)
            baseQuery = baseQuery.Where(e => e.CategoryId == categoryId.Value);

        if (paymentStatus.HasValue)
            baseQuery = baseQuery.Where(e => e.PaymentStatus == paymentStatus.Value);

        if (fortnightType.HasValue)
            baseQuery = baseQuery.Where(e => e.FortnightType == fortnightType.Value);

        if (sourceType.HasValue)
            baseQuery = baseQuery.Where(e => e.SourceType == sourceType.Value);

        var totalCount = await baseQuery.CountAsync(ct);

        // LEFT JOIN com ExpenseOrders — ordena pelo campo Order quando disponível,
        // itens sem ordem ficam ao final (ordenados por FortnightType/DueDate)
        var joined = from e in baseQuery
                     join eo in _context.ExpenseOrders on e.Id equals eo.ExpenseId into og
                     from eo in og.DefaultIfEmpty()
                     select new { Expense = e, Order = (int?)eo.Order };

        var items = await joined
            .OrderBy(x => x.Order == null ? 1 : 0)
            .ThenBy(x => x.Order)
            .ThenBy(x => x.Expense.FortnightType)
            .ThenBy(x => x.Expense.DueDate)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(x => x.Expense)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<bool> HasExpensesByCategoryAsync(
    Guid categoryId, CancellationToken ct = default)
    => await _context.Expenses
           .AnyAsync(e => e.CategoryId == categoryId, ct);
}
