using Microsoft.EntityFrameworkCore;
using PersonalFinance.Application.DTOs.Financial;
using PersonalFinance.Application.DTOs.Reports;
using PersonalFinance.Application.Interfaces;
using PersonalFinance.Domain.Enums;
using PersonalFinance.Infrastructure.Persistence.Context;

namespace PersonalFinance.Api.Tests.Integration
{
    /// <summary>
    /// Substitui o ReportRepository em testes de integração.
    /// O ReportRepository real usa Database.SqlQueryRaw na vw_PeriodSummary,
    /// que é um método relacional — incompatível com o provider InMemory.
    /// Este fake recalcula o resumo via LINQ nas entidades do DbContext,
    /// produzindo o mesmo resultado lógico da view SQL sem depender do provider.
    /// </summary>
    internal sealed class FakeReportRepository : IReportRepository
    {
        private readonly AppDbContext _context;

        public FakeReportRepository(AppDbContext context) => _context = context;

        public async Task<PeriodSummaryDto?> GetPeriodSummaryAsync(
            Guid periodId, Guid userId, CancellationToken ct = default)
        {
            // Verifica posse do período — IgnoreQueryFilters para acessar dados
            // mesmo sem o filtro SQL que a view real usa
            var period = await _context.Periods
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(p => p.Id == periodId && p.UserId == userId, ct);

            if (period is null) return null;

            // Calcula totais via LINQ — espelha exatamente a lógica da vw_PeriodSummary
            var expenses = await _context.Expenses
                .IgnoreQueryFilters()
                .Where(e => e.PeriodId == periodId && e.UserId == userId && e.DeletedAt == null)
                .ToListAsync(ct);

            var incomes = await _context.Incomes
                .IgnoreQueryFilters()
                .Where(i => i.PeriodId == periodId && i.UserId == userId && i.DeletedAt == null)
                .ToListAsync(ct);

            var totalIncome = incomes.Sum(i => i.Amount);
            var totalExpense = expenses.Sum(e => e.Amount);
            var totalPaid = expenses.Where(e => e.PaymentStatus == PaymentStatus.Paid).Sum(e => e.Amount);
            var totalOwed = expenses.Where(e => e.PaymentStatus is PaymentStatus.Pending or PaymentStatus.Partial).Sum(e => e.Amount);
            var totalFirstFortnight = expenses.Where(e => e.FortnightType == FortnightType.First).Sum(e => e.Amount);
            var totalSecondFortnight = expenses.Where(e => e.FortnightType == FortnightType.Second).Sum(e => e.Amount);

            return new PeriodSummaryDto(
                PeriodId: periodId,
                UserId: userId,
                Year: period.Year,
                Month: period.Month,
                TotalIncome: totalIncome,
                TotalExpense: totalExpense,
                TotalPaid: totalPaid,
                TotalOwed: totalOwed,
                TotalFirstFortnight: totalFirstFortnight,
                TotalSecondFortnight: totalSecondFortnight,
                Balance: totalIncome - totalExpense
            );
        }

        public async Task<ExpensesReportDto> GetExpensesReportAsync(
            Guid userId, int year, int? month, CancellationToken ct = default)
        {
            var shortYear = (short)year;

            var periods = await _context.Periods
                .IgnoreQueryFilters()
                .Where(p => p.UserId == userId && p.Year == shortYear && (month == null || p.Month == (byte)month.Value))
                .Select(p => p.Id)
                .ToListAsync(ct);

            var expenses = await _context.Expenses
                .IgnoreQueryFilters()
                .Where(e => e.UserId == userId && periods.Contains(e.PeriodId) && e.DeletedAt == null)
                .ToListAsync(ct);

            var categories = await _context.Categories
                .IgnoreQueryFilters()
                .Where(c => expenses.Select(e => e.CategoryId).Contains(c.Id))
                .ToDictionaryAsync(c => c.Id, ct);

            var items = expenses
                .GroupBy(e => e.CategoryId)
                .Select(g =>
                {
                    categories.TryGetValue(g.Key, out var cat);
                    return new ExpenseByCategoryItemDto(
                        g.Key,
                        cat?.Name ?? "Desconhecida",
                        cat?.Color ?? "#888888",
                        g.Sum(e => e.Amount));
                })
                .OrderByDescending(i => i.Total)
                .ToList();

            return new ExpensesReportDto(year, month, items);
        }

    }

}