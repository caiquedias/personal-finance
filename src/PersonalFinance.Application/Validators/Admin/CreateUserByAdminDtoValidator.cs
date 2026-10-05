using FluentValidation;
using PersonalFinance.Application.DTOs.Admin;

namespace PersonalFinance.Application.Validators.Admin;

/// <summary>Valida a criação de usuário pelo admin.</summary>
public sealed class CreateUserByAdminDtoValidator : AbstractValidator<CreateUserByAdminDto>
{
    public CreateUserByAdminDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("O nome do usuário é obrigatório.")
            .MaximumLength(100).WithMessage("O nome deve ter no máximo 100 caracteres.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("O e-mail do usuário é obrigatório.")
            .MaximumLength(200).WithMessage("O e-mail deve ter no máximo 200 caracteres.")
            .EmailAddress().WithMessage("O e-mail informado é inválido.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("A senha é obrigatória.")
            .MinimumLength(8).WithMessage("A senha deve ter no mínimo 8 caracteres.")
            .MaximumLength(128).WithMessage("A senha deve ter no máximo 128 caracteres.");
    }
}
