using FluentValidation;
using PersonalFinance.Application.DTOs.Admin;

namespace PersonalFinance.Application.Validators.Admin;

/// <summary>Valida a atualização de nome de usuário pelo admin (UserId vem da rota, mesclado antes do use case).</summary>
public sealed class UpdateUserByAdminDtoValidator : AbstractValidator<UpdateUserByAdminDto>
{
    public UpdateUserByAdminDtoValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("O usuário é obrigatório.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("O nome do usuário é obrigatório.")
            .MaximumLength(100).WithMessage("O nome deve ter no máximo 100 caracteres.");
    }
}
