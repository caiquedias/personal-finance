using Microsoft.EntityFrameworkCore;
using PersonalFinance.Application.Interfaces;
using PersonalFinance.Domain.Entities.Financial;
using PersonalFinance.Domain.Exceptions;
using PersonalFinance.Infrastructure.Persistence.Context;
using System.Text;

namespace PersonalFinance.Infrastructure.Services;

/// <summary>
/// Exporta os dados de um período (Incomes + Expenses) para CSV com encoding UTF-8 com BOM.
/// Contrato de colunas (imutável):
/// Type,PeriodYear,PeriodMonth,Description,Amount,Category,FortnightType,PaymentStatus,SourceType,DueDate,PaymentDate,Notes
/// </summary>
public sealed class CsvExportService : ICsvExportService
{
    private const string Header =
        "Type,PeriodYear,PeriodMonth,Description,Amount,Category,FortnightType,PaymentStatus,SourceType,DueDate,PaymentDate,Notes";

    private readonly AppDbContext _context;

    public CsvExportService(AppDbContext context) => _context = context;

    public async Task<string> ExportPeriodAsync(
        Guid periodId,
        Guid userId,
        CancellationToken ct = default)
    {
        // Valida que o período pertence ao usuário
        var period = await _context.Periods
            .FirstOrDefaultAsync(p => p.Id == periodId && p.UserId == userId, ct)
            ?? throw new DomainException(
                "Período não encontrado ou sem permissão de acesso.");

        var incomes = await _context.Incomes
            .Where(i => i.PeriodId == periodId && i.UserId == userId)
            .ToListAsync(ct);

        var expenses = await _context.Expenses
            .Where(e => e.PeriodId == periodId && e.UserId == userId)
            .ToListAsync(ct);

        // UTF-8 com BOM
        var bom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        var sb = new StringBuilder();

        // Inicia com o char BOM Unicode (U+FEFF) para representar o BOM como string
        sb.Append('﻿');
        sb.AppendLine(Header);

        foreach (var income in incomes)
            sb.AppendLine(BuildIncomeLine(income, period.Year, period.Month));

        foreach (var expense in expenses)
            sb.AppendLine(BuildExpenseLine(expense, period.Year, period.Month));

        return sb.ToString();
    }

    // ── Linha de Income ──────────────────────────────────────────────────────────
    // Type="Income", FortnightType vazio, PaymentStatus vazio, SourceType vazio,
    // DueDate vazio, PaymentDate vazio
    private static string BuildIncomeLine(Income income, short year, byte month)
    {
        return string.Join(",",
            "Income",
            year,
            month,
            EscapeCsv(income.Description),
            income.Amount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
            string.Empty,          // Category — Income não tem categoria
            string.Empty,          // FortnightType — vazio para Income
            string.Empty,          // PaymentStatus — vazio para Income
            string.Empty,          // SourceType — vazio para Income
            string.Empty,          // DueDate — vazio para Income
            string.Empty,          // PaymentDate — vazio para Income
            EscapeCsv(income.Notes)
        );
    }

    // ── Linha de Expense ─────────────────────────────────────────────────────────
    // SourceType preenchido para Expense; FortnightType, PaymentStatus, DueDate, PaymentDate preenchidos
    private static string BuildExpenseLine(Expense expense, short year, byte month)
    {
        return string.Join(",",
            "Expense",
            year,
            month,
            EscapeCsv(expense.Description),
            expense.Amount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
            string.Empty,          // Category — nome não disponível sem join; vazio por ora
            expense.FortnightType.ToString(),
            expense.PaymentStatus.ToString(),
            expense.SourceType.ToString(),
            expense.DueDate.ToString("yyyy-MM-dd"),
            expense.PaymentDate.HasValue
                ? expense.PaymentDate.Value.ToString("yyyy-MM-dd")
                : string.Empty,
            EscapeCsv(expense.Notes)
        );
    }

    /// <summary>
    /// Escapa campos CSV: envolve em aspas se o valor contiver vírgula, aspas ou quebra de linha.
    /// Retorna string vazia para null.
    /// </summary>
    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;

        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";

        return value;
    }
}
