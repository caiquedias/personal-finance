namespace PersonalFinance.Application.DTOs.Import;

/// <summary>
/// Lançamento extraído de um extrato bancário em PDF antes de persistir.
/// Amount tem sinal: débito negativo, crédito positivo.
/// </summary>
public sealed record ParsedStatementEntryDto(
    DateOnly EventDate,
    DateOnly PostingDate,
    string   RawType,
    string   Description,
    decimal  Amount
);
