using PersonalFinance.Domain.Enums;

namespace PersonalFinance.Application.DTOs.Import;

/// <summary>
/// Representa uma despesa extraída da planilha Excel antes de persistir no banco.
/// Usado como modelo intermediário entre o parser e o use case de importação.
/// </summary>
public sealed record ParsedExpenseDto(
    string        Description,
    decimal       Amount,
    SourceType    SourceType,
    FortnightType FortnightType,
    bool          IsPaid,
    DateOnly      DueDate,
    string        CategoryName
);
