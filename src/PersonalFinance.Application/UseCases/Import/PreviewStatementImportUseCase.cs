using PersonalFinance.Application.DTOs.Import;
using PersonalFinance.Application.Interfaces;
using PersonalFinance.Domain.Interfaces.Repositories;

namespace PersonalFinance.Application.UseCases.Import;

/// <summary>
/// Gera o preview de importação de um extrato PDF: classifica Receita/Despesa,
/// sugere categoria e sinaliza transferências internas e duplicatas. Não persiste nada.
/// </summary>
public sealed class PreviewStatementImportUseCase(
    IStatementParserService parser,
    ICategoryRepository categoryRepository,
    IPeriodRepository periodRepository,
    IExpenseRepository expenseRepository,
    IIncomeRepository incomeRepository,
    StatementEntryClassifier classifier)
{
    private sealed record ExistingRecord(Guid Id, string Description, decimal Amount, DateOnly Date);

    public async Task<StatementPreviewResultDto> ExecuteAsync(
        Stream pdfStream, string? password, DateOnly fromDate, Guid userId, CancellationToken ct = default)
    {
        var parsed = await parser.ParseAsync(pdfStream, password, ct);

        var discarded = 0;
        var entries = new List<ParsedStatementEntryDto>();
        foreach (var entry in parsed)
        {
            if (entry.PostingDate < fromDate) { discarded++; continue; }
            if (entry.Amount == 0m) continue;
            entries.Add(entry);
        }

        if (entries.Count == 0)
            return new StatementPreviewResultDto([], discarded);

        var categories = (await categoryRepository.GetByUserAsync(userId, ct)).ToList();

        // Carrega registros existentes uma vez por mês coberto
        var expensesByMonth = new Dictionary<(int, int), List<ExistingRecord>>();
        var incomesByMonth  = new Dictionary<(int, int), List<ExistingRecord>>();
        foreach (var month in entries.Select(e => (e.PostingDate.Year, e.PostingDate.Month)).Distinct())
        {
            var period = await periodRepository.GetByUserYearMonthAsync(userId, month.Year, month.Month, ct);
            if (period is null) continue;

            expensesByMonth[month] = (await expenseRepository.GetByPeriodAsync(period.Id, userId, ct))
                .Select(x => new ExistingRecord(x.Id, x.Description, x.Amount, x.PaymentDate ?? x.DueDate))
                .ToList();
            incomesByMonth[month] = (await incomeRepository.GetByPeriodAsync(period.Id, userId, ct))
                .Select(x => new ExistingRecord(x.Id, x.Description, x.Amount, x.ReceivedAt))
                .ToList();
        }

        var items = new List<StatementPreviewItemDto>(entries.Count);
        foreach (var entry in entries)
        {
            var isIncome = entry.Amount > 0m;
            var amount   = Math.Abs(entry.Amount);
            var key      = (entry.PostingDate.Year, entry.PostingDate.Month);
            var source   = isIncome ? incomesByMonth : expensesByMonth;

            ExistingRecord? duplicate = null;
            if (source.TryGetValue(key, out var existing))
            {
                duplicate = existing.FirstOrDefault(r =>
                    r.Date == entry.PostingDate
                    && r.Amount == amount
                    && string.Equals(r.Description, entry.Description, StringComparison.OrdinalIgnoreCase));
            }

            items.Add(new StatementPreviewItemDto(
                entry.PostingDate,
                entry.Description,
                amount,
                isIncome ? "Income" : "Expense",
                classifier.SuggestCategoryId(entry.Description, categories),
                classifier.IsInternalTransfer(entry.Description),
                duplicate is not null,
                duplicate?.Id));
        }

        return new StatementPreviewResultDto(items, discarded);
    }
}
