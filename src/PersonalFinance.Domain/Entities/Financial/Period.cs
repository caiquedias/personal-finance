using PersonalFinance.Domain.Entities.Shared;
using PersonalFinance.Domain.Exceptions;

namespace PersonalFinance.Domain.Entities.Financial;

/// <summary>
/// Período mensal de referência financeira.
/// Cada usuário possui no máximo um período por mês/ano (garantido por UNIQUE constraint).
/// Âncora central de Expense e Income.
/// </summary>
public sealed class Period : EntityBase
{
    private const int MinYear = 1900;

    // ── Propriedades ──────────────────────────────────────────────────────────

    /// <summary>Usuário dono do período.</summary>
    public Guid UserId { get; private set; }

    /// <summary>Ano de referência. Ex: 2026.</summary>
    public short Year { get; private set; }

    /// <summary>Mês de referência. Intervalo: 1–12.</summary>
    public byte Month { get; private set; }

    // ── EF Core ───────────────────────────────────────────────────────────────
    private Period() { }

    // ── Factory ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Cria um novo período mensal para o usuário informado.
    /// A unicidade por (UserId, Year, Month) é garantida por constraint no banco
    /// e validada no use case antes de chamar este método.
    /// </summary>
    public static Period Create(Guid userId, int year, int month)
    {
        if (userId == Guid.Empty)
            throw new DomainException("O UserId do período é obrigatório.");

        ValidateYear(year);
        ValidateMonth(month);

        return new Period
        {
            UserId = userId,
            Year   = (short)year,
            Month  = (byte)month
        };
    }

    // ── Validações privadas ───────────────────────────────────────────────────

    private static void ValidateYear(int year)
    {
        if (year < MinYear)
            throw new DomainException($"O ano do período é inválido: {year}. Mínimo permitido: {MinYear}.");
    }

    private static void ValidateMonth(int month)
    {
        if (month is < 1 or > 12)
            throw new DomainException($"O mês do período é inválido: {month}. Use valores entre 1 e 12.");
    }
}
