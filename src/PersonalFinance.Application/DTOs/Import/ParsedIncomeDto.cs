using PersonalFinance.Domain.Enums;

namespace PersonalFinance.Application.DTOs.Import;

/// <summary>
/// Representa uma receita extraída da planilha Excel antes de persistir no banco.
/// </summary>
public sealed record ParsedIncomeDto(
    string        Description,
    decimal       Amount,
    FortnightType FortnightType,
    DateOnly      ReceivedAt
);
