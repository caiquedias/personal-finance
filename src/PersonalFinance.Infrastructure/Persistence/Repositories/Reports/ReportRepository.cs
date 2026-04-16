using Microsoft.EntityFrameworkCore;
using PersonalFinance.Application.DTOs.Financial;
using PersonalFinance.Application.DTOs.Reports;
using PersonalFinance.Application.Interfaces;
using PersonalFinance.Infrastructure.Persistence.Context;

namespace PersonalFinance.Infrastructure.Persistence.Repositories.Reports
{
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
                PeriodId: row.PeriodId,
                UserId: row.UserId,
                Year: row.Year,          // short → int implícito
                Month: row.Month,         // byte → int implícito
                TotalIncome: row.TotalIncome,
                TotalExpense: row.TotalExpense,
                TotalPaid: row.TotalPaid,
                TotalOwed: row.TotalOwed,
                TotalFirstFortnight: row.TotalFirstFortnight,
                TotalSecondFortnight: row.TotalSecondFortnight,
                Balance: row.Balance
            );
        }

        public async Task<ExpensesReportDto> GetExpensesReportAsync(
            Guid userId,
            int year,
            int? month,
            CancellationToken ct = default)
        {
            var shortYear = (short)year;

            var items = await _context.Expenses
                .Join(_context.Periods,
                    e => e.PeriodId,
                    p => p.Id,
                    (e, p) => new { Expense = e, Period = p })
                .Where(x =>
                    x.Expense.UserId == userId &&
                    x.Period.Year == shortYear &&
                    (!month.HasValue || x.Period.Month == (byte)month.Value))
                .Join(_context.Categories,
                    x => x.Expense.CategoryId,
                    c => c.Id,
                    (x, c) => new { x.Expense, Category = c })
                .GroupBy(x => new { x.Expense.CategoryId, x.Category.Name, x.Category.Color })
                .Select(g => new ExpenseByCategoryItemDto(
                    g.Key.CategoryId,
                    g.Key.Name,
                    g.Key.Color,
                    g.Sum(x => x.Expense.Amount)))
                .OrderByDescending(i => i.Total)
                .ToListAsync(ct);

            return new ExpensesReportDto(year, month, items);
        }

        // Tipo interno para mapeamento do SQL raw — nunca exposto fora da Infrastructure
        private sealed class PeriodSummaryRaw
        {
            public Guid PeriodId { get; set; }
            public Guid UserId { get; set; }
            public short Year { get; set; }  // smallint no banco
            public byte Month { get; set; }  // tinyint no banco
            public decimal TotalIncome { get; set; }
            public decimal TotalExpense { get; set; }
            public decimal TotalPaid { get; set; }
            public decimal TotalOwed { get; set; }
            public decimal TotalFirstFortnight { get; set; }
            public decimal TotalSecondFortnight { get; set; }
            public decimal Balance { get; set; }
        }
    }
}
