using PersonalFinance.Domain.Enums;

namespace PersonalFinance.Application.DTOs.Financial;

// ── Period ────────────────────────────────────────────────────────────────────

public sealed record CreatePeriodDto(
    Guid UserId,
    int  Year,
    int  Month
);

public sealed record PeriodResponseDto(
    Guid   Id,
    Guid   UserId,
    int    Year,
    int    Month,
    bool   IsActive
);

// ── Expense ───────────────────────────────────────────────────────────────────

public sealed record CreateExpenseDto(
    Guid          PeriodId,
    Guid          UserId,
    Guid          CategoryId,
    SourceType    SourceType,
    FortnightType FortnightType,
    string        Description,
    decimal       Amount,
    DateOnly      DueDate,
    string?       Notes = null
);

public sealed record UpdateExpenseDto(
    Guid          Id,
    Guid          UserId,
    Guid          CategoryId,
    SourceType    SourceType,
    FortnightType FortnightType,
    string        Description,
    decimal       Amount,
    DateOnly      DueDate,
    string?       Notes,
    PaymentStatus Status
);

public sealed record ExpenseResponseDto(
    Guid          Id,
    Guid          PeriodId,
    Guid          UserId,
    Guid          CategoryId,
    SourceType    SourceType,
    FortnightType FortnightType,
    PaymentStatus PaymentStatus,
    string        Description,
    decimal       Amount,
    DateOnly      DueDate,
    DateOnly?     PaymentDate,
    string?       Notes,
    bool          IsActive
);

public sealed record ExpenseFilterDto(
    int            PageNumber    = 1,
    int            PageSize      = 20,
    string?        Description   = null,
    Guid?          CategoryId    = null,
    PaymentStatus? PaymentStatus = null,
    FortnightType? FortnightType = null,
    SourceType?    SourceType    = null
);

public sealed record IncomeFilterDto(
    int            PageNumber    = 1,
    int            PageSize      = 20,
    string?        Description   = null,
    FortnightType? FortnightType = null
);

// ── Income ────────────────────────────────────────────────────────────────────

public sealed record CreateIncomeDto(
    Guid          PeriodId,
    Guid          UserId,
    FortnightType FortnightType,
    string        Description,
    decimal       Amount,
    DateOnly      ReceivedAt,
    string?       Notes = null
);

public sealed record IncomeResponseDto(
    Guid          Id,
    Guid          PeriodId,
    Guid          UserId,
    FortnightType FortnightType,
    string        Description,
    decimal       Amount,
    DateOnly      ReceivedAt,
    string?       Notes,
    bool          IsActive
);

// ── Period Summary (view) ─────────────────────────────────────────────────────

/// <summary>
/// Projeção da vw_PeriodSummary.
/// Calculada em tempo real — carregada por demanda.
/// Balance = TotalIncome - TotalExpense.
/// </summary>
public sealed record PeriodSummaryDto(
    Guid    PeriodId,
    Guid    UserId,
    int     Year,
    int     Month,
    decimal TotalIncome,
    decimal TotalExpense,
    decimal TotalPaid,
    decimal TotalOwed,
    decimal TotalFirstFortnight,
    decimal TotalSecondFortnight,
    decimal Balance
);
