using FluentValidation;
using PersonalFinance.Application.DTOs.Admin;

namespace PersonalFinance.Application.Validators.Admin;

/// <summary>Valida o reset de senha pelo admin (UserId vem da rota, mesclado antes do use case).</summary>
public sealed class ResetPasswordDtoValidator : AbstractValidator<ResetPasswordDto>
{
    public ResetPasswordDtoValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("O usuário é obrigatório.");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("A nova senha é obrigatória.")
            .MinimumLength(8).WithMessage("A nova senha deve ter no mínimo 8 caracteres.")
            .MaximumLength(128).WithMessage("A nova senha deve ter no máximo 128 caracteres.");
    }
}
