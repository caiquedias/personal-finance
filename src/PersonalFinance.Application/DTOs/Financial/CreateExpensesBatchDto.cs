namespace PersonalFinance.Application.DTOs.Financial;

public sealed record CreateExpensesBatchDto(
    Guid                               PeriodId,
    Guid                               UserId,
    IReadOnlyList<BatchExpenseItemDto> Items
);
