namespace PersonalFinance.Application.DTOs.Reports;

/// <summary>
/// Item de despesa agrupado por categoria.
/// </summary>
public sealed record ExpenseByCategoryItemDto(
    Guid    CategoryId,
    string  CategoryName,
    string  CategoryColor,
    decimal Total
);

/// <summary>
/// Resultado do relatório de despesas.
/// Month nulo → agregação anual por categoria (Gráfico 1 — pizza).
/// Month preenchido → agregação do mês por categoria (Gráfico 2 — barras).
/// </summary>
public sealed record ExpensesReportDto(
    int                                    Year,
    int?                                   Month,
    IReadOnlyList<ExpenseByCategoryItemDto> Items
);
