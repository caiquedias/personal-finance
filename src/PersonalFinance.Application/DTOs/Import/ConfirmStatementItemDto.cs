namespace PersonalFinance.Application.DTOs.Import;

/// <summary>
/// Lançamento revisado pelo usuário a ser persistido. Kind é "Income" ou "Expense".
/// SourceType é opcional (padrão Personal).
/// </summary>
public sealed record ConfirmStatementItemDto(
    DateOnly Date,
    string   Description,
    decimal  Amount,
    string   Kind,
    Guid?    CategoryId,
    string?  SourceType
);
