using FluentValidation;
using PersonalFinance.Application.DTOs.Admin;

namespace PersonalFinance.Application.Validators.Config;

/// <summary>Valida a atualização de status de pagamento (limites alinhados às colunas do banco).</summary>
public sealed class UpdatePaymentStatusDtoValidator : AbstractValidator<UpdatePaymentStatusDto>
{
    public UpdatePaymentStatusDtoValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0).WithMessage("O identificador é obrigatório.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("O nome é obrigatório.")
            .MaximumLength(50).WithMessage("O nome deve ter no máximo 50 caracteres.");

        RuleFor(x => x.Description)
            .MaximumLength(200).WithMessage("A descrição deve ter no máximo 200 caracteres.");
    }
}
