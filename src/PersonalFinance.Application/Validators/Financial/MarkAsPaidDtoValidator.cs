using FluentValidation;
using PersonalFinance.Application.DTOs.Financial;

namespace PersonalFinance.Application.Validators.Financial;

/// <summary>Valida a marcação de despesa como paga: data obrigatória e não futura (UTC).</summary>
public sealed class MarkAsPaidDtoValidator : AbstractValidator<MarkAsPaidDto>
{
    public MarkAsPaidDtoValidator()
    {
        RuleFor(x => x.PaymentDate)
            .NotEqual(default(DateOnly)).WithMessage("A data de pagamento é obrigatória.")
            .Must(d => d <= DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("A data de pagamento não pode ser futura.");
    }
}
