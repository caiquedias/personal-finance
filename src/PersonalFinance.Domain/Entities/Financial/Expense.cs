using PersonalFinance.Domain.Entities.Shared;
using PersonalFinance.Domain.Enums;
using PersonalFinance.Domain.Exceptions;

namespace PersonalFinance.Domain.Entities.Financial;

/// <summary>
/// Despesa registrada em um período mensal.
/// Invariantes:
///   - Amount deve ser maior que zero
///   - PaymentDate só pode ser preenchida com data não futura
///   - Status transiciona via métodos (MarkAsPaid, MarkAsCancelled, MarkAsPartial)
/// </summary>
public sealed class Expense : EntityBase
{
    // ── Propriedades ──────────────────────────────────────────────────────────

    /// <summary>Período ao qual a despesa pertence.</summary>
    public Guid PeriodId { get; private set; }

    /// <summary>Usuário dono da despesa.</summary>
    public Guid UserId { get; private set; }

    /// <summary>Categoria da despesa.</summary>
    public Guid CategoryId { get; private set; }

    /// <summary>Origem da despesa: Parental ou Personal.</summary>
    public SourceType SourceType { get; private set; }

    /// <summary>Quinzena do lançamento: First ou Second.</summary>
    public FortnightType FortnightType { get; private set; }

    /// <summary>Status de pagamento atual da despesa.</summary>
    public PaymentStatus PaymentStatus { get; private set; }

    /// <summary>Descrição da despesa.</summary>
    public string Description { get; private set; } = default!;

    /// <summary>Valor da despesa. Sempre maior que zero.</summary>
    public decimal Amount { get; private set; }

    /// <summary>Data de vencimento.</summary>
    public DateOnly DueDate { get; private set; }

    /// <summary>Data de pagamento efetivo. NULL quando ainda não pago.</summary>
    public DateOnly? PaymentDate { get; private set; }

    /// <summary>Observações livres. Nullable.</summary>
    public string? Notes { get; private set; }

    // ── EF Core ───────────────────────────────────────────────────────────────
    private Expense() { }

    // ── Factory ───────────────────────────────────────────────────────────────

    public static Expense Create(
        Guid periodId,
        Guid userId,
        Guid categoryId,
        SourceType sourceType,
        FortnightType fortnightType,
        PaymentStatus paymentStatus,
        string description,
        decimal amount,
        DateOnly dueDate,
        DateOnly? paymentDate,
        string? notes)
    {
        ValidateId(periodId,   "O PeriodId da despesa é obrigatório.");
        ValidateId(userId,     "O UserId da despesa é obrigatório.");
        ValidateId(categoryId, "O CategoryId da despesa é obrigatório.");
        ValidateDescription(description);
        ValidateAmount(amount);

        return new Expense
        {
            PeriodId      = periodId,
            UserId        = userId,
            CategoryId    = categoryId,
            SourceType    = sourceType,
            FortnightType = fortnightType,
            PaymentStatus = paymentStatus,
            Description   = description.Trim(),
            Amount        = amount,
            DueDate       = dueDate,
            PaymentDate   = paymentDate,
            Notes         = notes?.Trim()
        };
    }

    // ── Comportamentos de status ──────────────────────────────────────────────

    /// <summary>
    /// Marca a despesa como paga.
    /// A data de pagamento não pode ser futura.
    /// </summary>
    public void MarkAsPaid(DateOnly paymentDate)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        if (paymentDate > today)
            throw new DomainException(
                "A data de pagamento não pode ser uma data futura.");

        PaymentStatus = PaymentStatus.Paid;
        PaymentDate   = paymentDate;
        SetUpdatedAt();
    }

    /// <summary>Marca a despesa como cancelada. PaymentDate é removida.</summary>
    public void MarkAsCancelled()
    {
        PaymentStatus = PaymentStatus.Cancelled;
        PaymentDate   = null;
        SetUpdatedAt();
    }

    /// <summary>Marca a despesa como parcialmente paga.</summary>
    public void MarkAsPartial()
    {
        PaymentStatus = PaymentStatus.Partial;
        SetUpdatedAt();
    }

    /// <summary>Marca a despesa como pendente.</summary>
    public void MarkAsPending()
    {
        PaymentStatus = PaymentStatus.Pending;
        PaymentDate   = null;
        SetUpdatedAt();
    }

    // ── Atualização ───────────────────────────────────────────────────────────

    /// <summary>Atualiza os dados editáveis da despesa.</summary>
    public void Update(
        Guid categoryId,
        SourceType sourceType,
        FortnightType fortnightType,
        string description,
        decimal amount,
        DateOnly dueDate,
        string? notes)
    {
        ValidateId(categoryId, "O CategoryId da despesa é obrigatório.");
        ValidateDescription(description);
        ValidateAmount(amount);

        CategoryId    = categoryId;
        SourceType    = sourceType;
        FortnightType = fortnightType;
        Description   = description.Trim();
        Amount        = amount;
        DueDate       = dueDate;
        Notes         = notes?.Trim();
        SetUpdatedAt();
    }

    // ── Validações privadas ───────────────────────────────────────────────────

    private static void ValidateId(Guid id, string message)
    {
        if (id == Guid.Empty)
            throw new DomainException(message);
    }

    private static void ValidateDescription(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
            throw new DomainException("A descrição da despesa é obrigatória.");

        if (description.Trim().Length > 200)
            throw new DomainException("A descrição da despesa não pode exceder 200 caracteres.");
    }

    private static void ValidateAmount(decimal amount)
    {
        if (amount <= 0)
            throw new DomainException(
                $"O valor da despesa deve ser maior que zero. Valor informado: {amount}.");
    }
}
