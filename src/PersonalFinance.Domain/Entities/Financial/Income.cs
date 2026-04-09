using PersonalFinance.Domain.Entities.Shared;
using PersonalFinance.Domain.Enums;
using PersonalFinance.Domain.Exceptions;

namespace PersonalFinance.Domain.Entities.Financial;

public sealed class Income : EntityBase
{
    public Guid PeriodId { get; private set; }
    public Guid UserId { get; private set; }
    public FortnightType FortnightType { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }
    public DateOnly ReceivedAt { get; private set; }
    public string? Notes { get; private set; }

    private Income() { }

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

        if (string.IsNullOrWhiteSpace(description))
            throw new DomainException("A descrição da receita é obrigatória.");

        if (amount <= 0)
            throw new DomainException("O valor da receita deve ser maior que zero.");

        return new Income
        {
            PeriodId = periodId,
            UserId = userId,
            FortnightType = fortnightType,
            Description = description.Trim(),
            Amount = amount,
            ReceivedAt = receivedAt,
            Notes = notes?.Trim(),
        };
    }

    /// <summary>
    /// Atualiza os campos mutáveis da receita.
    /// Usado pelo upsert de importação quando os dados da planilha mudaram.
    /// </summary>
    public void Update(
        FortnightType fortnightType,
        string description,
        decimal amount,
        DateOnly receivedAt,
        string? notes)
    {
        if (string.IsNullOrWhiteSpace(description))
            throw new DomainException("A descrição da receita é obrigatória.");

        if (amount <= 0)
            throw new DomainException("O valor da receita deve ser maior que zero.");

        FortnightType = fortnightType;
        Description = description.Trim();
        Amount = amount;
        ReceivedAt = receivedAt;
        Notes = notes?.Trim();
        SetUpdatedAt();
    }
}
