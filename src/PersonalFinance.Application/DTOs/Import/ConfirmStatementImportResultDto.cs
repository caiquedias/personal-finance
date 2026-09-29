namespace PersonalFinance.Application.DTOs.Import;

public sealed record ConfirmStatementImportResultDto(
    int PeriodsCreated,
    int PeriodsReused,
    int ExpensesCreated,
    int IncomesCreated
);
