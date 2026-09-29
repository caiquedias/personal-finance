namespace PersonalFinance.Application.DTOs.Import;

/// <summary>Resultado do preview de importação de extrato (nada é persistido).</summary>
public sealed record StatementPreviewResultDto(
    IReadOnlyList<StatementPreviewItemDto> Items,
    int DiscardedByDateCount
);
