namespace PersonalFinance.Application.DTOs.Import;

/// <summary>
/// Sugestão de lançamento gerada a partir do extrato, para revisão manual antes de salvar.
/// Amount é sempre absoluto; Kind é "Income" ou "Expense".
/// </summary>
public sealed record StatementPreviewItemDto(
    DateOnly Date,
    string   Description,
    decimal  Amount,
    string   Kind,
    Guid?    SuggestedCategoryId,
    bool     IsLikelyInternalTransfer,
    bool     IsLikelyDuplicate,
    Guid?    DuplicateOfId
);
