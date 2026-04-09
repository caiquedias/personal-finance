namespace PersonalFinance.Application.DTOs.Import;

/// <summary>
/// Sumário do resultado da importação com suporte a upsert.
/// PeriodsSkipped agora representa períodos reutilizados (não duplicados).
/// </summary>
public sealed record ImportResultDto(
    int                   PeriodsCreated,
    int                   PeriodsSkipped,
    int                   CategoriesCreated,
    int                   ExpensesImported,
    int                   IncomesImported,
    int                   ExpensesSkipped,
    int                   IncomesSkipped,
    IReadOnlyList<string> Warnings
);
