namespace PersonalFinance.Application.DTOs.Import;

/// <summary>
/// Representa o conteúdo completo de uma aba da planilha após o parsing.
/// Cada aba corresponde a um período mensal.
/// </summary>
public sealed record ParsedSheetDto(
    int                          Year,
    int                          Month,
    IReadOnlyList<ParsedIncomeDto>  Incomes,
    IReadOnlyList<ParsedExpenseDto> Expenses,
    IReadOnlyList<string>           Warnings
);
