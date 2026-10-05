namespace PersonalFinance.Application.DTOs.Financial;

/// <summary>DTO para o endpoint PATCH /expenses/{id}/pay</summary>
public sealed record MarkAsPaidDto(DateOnly PaymentDate);
