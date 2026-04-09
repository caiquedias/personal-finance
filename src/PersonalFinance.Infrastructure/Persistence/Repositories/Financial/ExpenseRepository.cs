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

    public async Task<bool> HasExpensesByCategoryAsync(
    Guid categoryId, CancellationToken ct = default)
    => await _context.Expenses
           .AnyAsync(e => e.CategoryId == categoryId, ct);
}
