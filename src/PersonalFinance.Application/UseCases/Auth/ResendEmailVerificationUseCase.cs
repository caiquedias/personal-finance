using FluentValidation;
using PersonalFinance.Application.DTOs.Auth;
using PersonalFinance.Application.Services.Auth;
using PersonalFinance.Domain.Enums;
using PersonalFinance.Domain.Interfaces.Repositories;

namespace PersonalFinance.Application.UseCases.Auth;

/// <summary>
/// Reenvia o código de verificação de e-mail. Anti-enumeração: nunca lança nem difere o resultado para
/// e-mail inexistente, inativo, removido ou já confirmado (nada é emitido nem enfileirado).
/// </summary>
public sealed class ResendEmailVerificationUseCase
{
    private readonly IUserRepository _users;
    private readonly UserTokenIssuer _issuer;
    private readonly IValidator<EmailRequestDto> _validator;

    public ResendEmailVerificationUseCase(
        IUserRepository users,
        UserTokenIssuer issuer,
        IValidator<EmailRequestDto> validator)
    {
        _users = users;
        _issuer = issuer;
        _validator = validator;
    }

    public async Task ExecuteAsync(EmailRequestDto dto, CancellationToken ct = default)
    {
        await _validator.ValidateAndThrowAsync(dto, ct);

        var email = dto.Email.Trim().ToLowerInvariant();
        var user = await _users.GetByEmailAsync(email, ct);

        if (user is null || !user.IsActive || user.IsDeleted || user.IsEmailConfirmed)
            return;

        // false = cooldown: sem token novo e sem e-mail; a resposta ao chamador é a mesma
        await _issuer.IssueAsync(user, UserTokenPurpose.EmailVerification, ct);
    }
}
