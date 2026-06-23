using PersonalFinance.Domain.Entities.Shared;
using PersonalFinance.Domain.Exceptions;

namespace PersonalFinance.Domain.Entities.Financial;

/// <summary>
/// Registro de expurgo de um período mensal.
/// Armazena snapshot dos totais e referência ao CSV gerado antes da exclusão física.
/// </summary>
public sealed class PurgeRecord : EntityBase
{
    // ── Propriedades ──────────────────────────────────────────────────────────

    /// <summary>Usuário dono do expurgo.</summary>
    public Guid UserId { get; private set; }

    /// <summary>Ano do período expurgado.</summary>
    public short PeriodYear { get; private set; }

    /// <summary>Mês do período expurgado.</summary>
    public byte PeriodMonth { get; private set; }

    /// <summary>Data/hora UTC do expurgo.</summary>
    public DateTime PurgedAt { get; private set; }

    /// <summary>Total de receitas do período expurgado.</summary>
    public decimal TotalIncome { get; private set; }

    /// <summary>Total de despesas do período expurgado.</summary>
    public decimal TotalExpense { get; private set; }

    /// <summary>Quantidade de despesas do período expurgado.</summary>
    public int ExpenseCount { get; private set; }

    /// <summary>Quantidade de receitas do período expurgado.</summary>
    public int IncomeCount { get; private set; }

    /// <summary>JSON com resumo de despesas por categoria (CategoryId → total).</summary>
    public string CategorySummaryJson { get; private set; } = default!;

    /// <summary>Nome do arquivo CSV gerado no expurgo.</summary>
    public string CsvFileName { get; private set; } = default!;

    // ── EF Core ───────────────────────────────────────────────────────────────
    private PurgeRecord() { }

    // ── Factory ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Cria um novo registro de expurgo com snapshot dos dados do período.
    /// </summary>
    public static PurgeRecord Create(
        Guid userId,
        int periodYear,
        int periodMonth,
        decimal totalIncome,
        decimal totalExpense,
        int incomeCount,
        int expenseCount,
        string categorySummaryJson,
        string csvFileName)
    {
        if (userId == Guid.Empty)
            throw new DomainException("O UserId do expurgo é obrigatório.");

        if (periodMonth < 1 || periodMonth > 12)
            throw new DomainException($"O mês do período é inválido: {periodMonth}. Use valores entre 1 e 12.");

        if (periodYear < 2000)
            throw new DomainException($"O ano do período é inválido: {periodYear}. Mínimo permitido: 2000.");

        if (totalIncome < 0)
            throw new DomainException("O total de receitas não pode ser negativo.");

        if (totalExpense < 0)
            throw new DomainException("O total de despesas não pode ser negativo.");

        if (expenseCount < 0)
            throw new DomainException("A quantidade de despesas não pode ser negativa.");

        if (incomeCount < 0)
            throw new DomainException("A quantidade de receitas não pode ser negativa.");

        if (string.IsNullOrWhiteSpace(csvFileName))
            throw new DomainException("O nome do arquivo CSV é obrigatório.");

        if (string.IsNullOrWhiteSpace(categorySummaryJson))
            throw new DomainException("O resumo de categorias é obrigatório.");

        return new PurgeRecord
        {
            UserId              = userId,
            PeriodYear          = (short)periodYear,
            PeriodMonth         = (byte)periodMonth,
            PurgedAt            = DateTime.UtcNow,
            TotalIncome         = totalIncome,
            TotalExpense        = totalExpense,
            ExpenseCount        = expenseCount,
            IncomeCount         = incomeCount,
            CategorySummaryJson = categorySummaryJson,
            CsvFileName         = csvFileName
        };
    }
}
