using Microsoft.EntityFrameworkCore;
using PersonalFinance.Application.DTOs.Financial;
using PersonalFinance.Application.Interfaces;
using PersonalFinance.Domain.Entities.Financial;
using PersonalFinance.Domain.Enums;
using PersonalFinance.Domain.Interfaces.Repositories;
using PersonalFinance.Infrastructure.Persistence.Context;

namespace PersonalFinance.Infrastructure.Persistence.Repositories.Financial;

// ══════════════════════════════════════════════════════════════════════════════
// PERIOD
// ══════════════════════════════════════════════════════════════════════════════

public sealed class PeriodRepository : IPeriodRepository
{
    private readonly AppDbContext _context;

    public PeriodRepository(AppDbContext context) => _context = context;

    public async Task<Period?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.Periods.FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<Period?> GetByIdAndUserAsync(
        Guid id, Guid userId, CancellationToken ct = default)
        => await _context.Periods
               .FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId, ct);

    public async Task<Period?> GetByYearMonthAsync(
        Guid userId, int year, int month, CancellationToken ct = default)
        => await _context.Periods
               .FirstOrDefaultAsync(
                   p => p.UserId == userId && p.Year == year && p.Month == month, ct);

    public async Task<IEnumerable<Period>> GetByUserAsync(
        Guid userId, CancellationToken ct = default)
        => await _context.Periods
               .Where(p => p.UserId == userId)
               .OrderByDescending(p => p.Year)
               .ThenByDescending(p => p.Month)
               .ToListAsync(ct);

    public async Task<bool> ExistsAsync(
        Guid userId, int year, int month, CancellationToken ct = default)
        => await _context.Periods
               .AnyAsync(p => p.UserId == userId && p.Year == year && p.Month == month, ct);

    public async Task<bool> ExistsByIdAndUserAsync(
        Guid periodId, Guid userId, CancellationToken ct = default)
        => await _context.Periods
               .AnyAsync(p => p.Id == periodId && p.UserId == userId, ct);

    public async Task AddAsync(Period period, CancellationToken ct = default)
        => await _context.Periods.AddAsync(period, ct);
}

// ══════════════════════════════════════════════════════════════════════════════
// EXPENSE
// ══════════════════════════════════════════════════════════════════════════════

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
               .OrderBy(e => e.DueDate)
               .ToListAsync(ct);

    public async Task<bool> HasExpensesByCategoryAsync(
        Guid categoryId, CancellationToken ct = default)
        => await _context.Expenses
               .AnyAsync(e => e.CategoryId == categoryId, ct);

    public async Task AddAsync(Expense expense, CancellationToken ct = default)
        => await _context.Expenses.AddAsync(expense, ct);

    public Task UpdateAsync(Expense expense, CancellationToken ct = default)
    {
        _context.Expenses.Update(expense);
        return Task.CompletedTask;
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// INCOME
// ══════════════════════════════════════════════════════════════════════════════

public sealed class IncomeRepository : IIncomeRepository
{
    private readonly AppDbContext _context;

    public IncomeRepository(AppDbContext context) => _context = context;

    public async Task<Income?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.Incomes.FirstOrDefaultAsync(i => i.Id == id, ct);

    public async Task<Income?> GetByIdAndUserAsync(
        Guid id, Guid userId, CancellationToken ct = default)
        => await _context.Incomes
               .FirstOrDefaultAsync(i => i.Id == id && i.UserId == userId, ct);

    public async Task<IEnumerable<Income>> GetByPeriodAsync(
        Guid periodId, Guid userId, CancellationToken ct = default)
        => await _context.Incomes
               .Where(i => i.PeriodId == periodId && i.UserId == userId)
               .OrderBy(i => i.ReceivedAt)
               .ToListAsync(ct);

    public async Task AddAsync(Income income, CancellationToken ct = default)
        => await _context.Incomes.AddAsync(income, ct);

    public Task UpdateAsync(Income income, CancellationToken ct = default)
    {
        _context.Incomes.Update(income);
        return Task.CompletedTask;
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// REPORT — vw_PeriodSummary
// ══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Implementação do IReportRepository.
/// Consulta a vw_PeriodSummary via SQL raw — a view não é mapeada como DbSet
/// para deixar explícito que é somente leitura e não gerenciada pelo EF Core.
/// </summary>
public sealed class ReportRepository : IReportRepository
{
    private readonly AppDbContext _context;

    public ReportRepository(AppDbContext context) => _context = context;

    public async Task<PeriodSummaryDto?> GetPeriodSummaryAsync(
        Guid periodId,
        Guid userId,
        CancellationToken ct = default)
    {
        // SQL raw na view — sem tabela física, sem tracking
        var sql = @"
            SELECT
                PeriodId,
                UserId,
                Year,
                Month,
                TotalIncome,
                TotalExpense,
                TotalPaid,
                TotalOwed,
                TotalFirstFortnight,
                TotalSecondFortnight,
                Balance
            FROM dbo.vw_PeriodSummary
            WHERE PeriodId = {0}
              AND UserId   = {1}";

        var results = await _context.Database
            .SqlQueryRaw<PeriodSummaryRaw>(sql, periodId, userId)
            .ToListAsync(ct);

        var row = results.FirstOrDefault();
        if (row is null) return null;

        return new PeriodSummaryDto(
            PeriodId:             row.PeriodId,
            UserId:               row.UserId,
            Year:                 row.Year,
            Month:                row.Month,
            TotalIncome:          row.TotalIncome,
            TotalExpense:         row.TotalExpense,
            TotalPaid:            row.TotalPaid,
            TotalOwed:            row.TotalOwed,
            TotalFirstFortnight:  row.TotalFirstFortnight,
            TotalSecondFortnight: row.TotalSecondFortnight,
            Balance:              row.Balance
        );
    }

    // Tipo interno para mapeamento do SQL raw — nunca exposto fora da Infrastructure
    private sealed class PeriodSummaryRaw
    {
        public Guid    PeriodId             { get; set; }
        public Guid    UserId               { get; set; }
        public int     Year                 { get; set; }
        public int     Month                { get; set; }
        public decimal TotalIncome          { get; set; }
        public decimal TotalExpense         { get; set; }
        public decimal TotalPaid            { get; set; }
        public decimal TotalOwed            { get; set; }
        public decimal TotalFirstFortnight  { get; set; }
        public decimal TotalSecondFortnight { get; set; }
        public decimal Balance              { get; set; }
    }
}
