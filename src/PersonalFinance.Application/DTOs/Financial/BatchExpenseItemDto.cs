using PersonalFinance.Domain.Enums;

namespace PersonalFinance.Application.DTOs.Financial;

public sealed record BatchExpenseItemDto(
    Guid          CategoryId,
    SourceType    SourceType,
    FortnightType FortnightType,
    string        Description,
    decimal       Amount,
    DateOnly      DueDate,
    string?       Notes       = null,
    bool          IsRecurring = false
);
