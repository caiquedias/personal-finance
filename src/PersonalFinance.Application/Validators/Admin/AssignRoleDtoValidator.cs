using FluentValidation;
using PersonalFinance.Application.DTOs.Admin;

namespace PersonalFinance.Application.Validators.Admin;

/// <summary>Valida a atribuição de role (UserId vem da rota, mesclado antes do use case).</summary>
public sealed class AssignRoleDtoValidator : AbstractValidator<AssignRoleDto>
{
    public AssignRoleDtoValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("O usuário é obrigatório.");

        RuleFor(x => x.RoleId)
            .GreaterThan(0).WithMessage("A role informada é inválida.");
    }
}
