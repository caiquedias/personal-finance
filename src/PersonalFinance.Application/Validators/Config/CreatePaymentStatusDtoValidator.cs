using FluentValidation;
using PersonalFinance.Application.DTOs.Admin;

namespace PersonalFinance.Application.Validators.Config;

/// <summary>Valida a criação de status de pagamento (limites alinhados às colunas do banco).</summary>
public sealed class CreatePaymentStatusDtoValidator : AbstractValidator<CreatePaymentStatusDto>
{
    public CreatePaymentStatusDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("O nome é obrigatório.")
            .MaximumLength(50).WithMessage("O nome deve ter no máximo 50 caracteres.");

        RuleFor(x => x.Description)
            .MaximumLength(200).WithMessage("A descrição deve ter no máximo 200 caracteres.");
    }
}
