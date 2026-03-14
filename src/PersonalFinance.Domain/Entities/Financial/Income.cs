using PersonalFinance.Domain.Entities.Shared;
using PersonalFinance.Domain.Enums;
using PersonalFinance.Domain.Exceptions;

namespace PersonalFinance.Domain.Entities.Financial;

/// <summary>
/// Receita registrada em um período mensal.
/// Representa entradas financeiras: salário, adiantamento, 13º, PLR, etc.
/// Invariante: Amount deve ser maior que zero.
/// </summary>
public sealed class Income : EntityBase
{
    // ── Propriedades ──────────────────────────────────────────────────────────

    /// <summary>Período ao qual a receita pertence.</summary>
    public Guid PeriodId { get; private set; }

    /// <summary>Usuário dono da receita.</summary>
    public Guid UserId { get; private set; }

    /// <summary>Quinzena do recebimento: First ou Second.</summary>
    public FortnightType FortnightType { get; private set; }

    /// <summary>Descrição da receita. Ex: "Adiantamento MDS", "Saldo mensal".</summary>
    public string Description { get; private set; } = default!;

    /// <summary>Valor recebido. Sempre maior que zero.</summary>
    public decimal Amount { get; private set; }

    /// <summary>Data do recebimento efetivo.</summary>
    public DateOnly ReceivedAt { get; private set; }

    /// <summary>Observações livres. Nullable.</summary>
    public string? Notes { get; private set; }

    // ── EF Core ───────────────────────────────────────────────────────────────
    private Income() { }

    // ── Factory ───────────────────────────────────────────────────────────────

    public static Income Create(
        Guid periodId,
        Guid userId,
        FortnightType fortnightType,
        string description,
        decimal amount,
        DateOnly receivedAt,
        string? notes)
    {
        if (periodId == Guid.Empty)
            throw new DomainException("O PeriodId da receita é obrigatório.");

        if (userId == Guid.Empty)
            throw new DomainException("O UserId da receita é obrigatório.");

        ValidateDescription(description);
        ValidateAmount(amount);

        return new Income
        {
            PeriodId      = periodId,
            UserId        = userId,
            FortnightType = fortnightType,
            Description   = description.Trim(),
            Amount        = amount,
            ReceivedAt    = receivedAt,
            Notes         = notes?.Trim()
        };
    }

    // ── Atualização ───────────────────────────────────────────────────────────

    /// <summary>Atualiza os dados editáveis da receita.</summary>
    public void Update(
        FortnightType fortnightType,
        string description,
        decimal amount,
        DateOnly receivedAt,
        string? notes)
    {
        ValidateDescription(description);
        ValidateAmount(amount);

        FortnightType = fortnightType;
        Description   = description.Trim();
        Amount        = amount;
        ReceivedAt    = receivedAt;
        Notes         = notes?.Trim();
        SetUpdatedAt();
    }

    // ── Validações privadas ───────────────────────────────────────────────────

    private static void ValidateDescription(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
            throw new DomainException("A descrição da receita é obrigatória.");

        if (description.Trim().Length > 200)
            throw new DomainException("A descrição da receita não pode exceder 200 caracteres.");
    }

    private static void ValidateAmount(decimal amount)
    {
        if (amount <= 0)
            throw new DomainException(
                $"O valor da receita deve ser maior que zero. Valor informado: {amount}.");
    }
}
