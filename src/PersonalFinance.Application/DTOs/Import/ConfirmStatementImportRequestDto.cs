namespace PersonalFinance.Application.DTOs.Import;

/// <summary>Lista final de lançamentos a persistir (pode cruzar meses).</summary>
public sealed record ConfirmStatementImportRequestDto(
    IReadOnlyList<ConfirmStatementItemDto> Items
);
