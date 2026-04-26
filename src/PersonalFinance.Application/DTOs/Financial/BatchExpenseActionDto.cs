namespace PersonalFinance.Application.DTOs.Financial;

public sealed record BatchExpenseActionDto(
    IReadOnlyList<Guid> ExpenseIds
);
