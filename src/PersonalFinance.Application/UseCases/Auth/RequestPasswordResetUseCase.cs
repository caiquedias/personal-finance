using FluentValidation;
using PersonalFinance.Application.DTOs.Auth;
using PersonalFinance.Application.Services.Auth;
using PersonalFinance.Domain.Entities.Auth;
using PersonalFinance.Domain.Enums;
using PersonalFinance.Domain.Interfaces.Repositories;

namespace PersonalFinance.Application.UseCases.Auth;

/// <summary>
/// Pede o código de reset de senha. Anti-enumeração: nunca lança nem difere o resultado para
/// e-mail inexistente, inativo ou removido (nada é emitido, enfileirado nem auditado).
/// </summary>
public sealed class RequestPasswordResetUseCase
{
    private readonly IUserRepository _users;
    private readonly UserTokenIssuer _issuer;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _uow;
    private readonly IValidator<EmailRequestDto> _validator;

    public RequestPasswordResetUseCase(
        IUserRepository users,
        UserTokenIssuer issuer,
        IAuditLogRepository audit,
        IUnitOfWork uow,
        IValidator<EmailRequestDto> validator)
    {
        _users = users;
        _issuer = issuer;
        _audit = audit;
        _uow = uow;
        _validator = validator;
    }

    public async Task ExecuteAsync(EmailRequestDto dto, string? ipAddress, CancellationToken ct = default)
    {
        await _validator.ValidateAndThrowAsync(dto, ct);

        var email = dto.Email.Trim().ToLowerInvariant();
        var user = await _users.GetByEmailAsync(email, ct);

        if (user is null || !user.IsActive || user.IsDeleted)
            return;

        // Auditoria entra na mesma transação do commit do issuer (que persiste o token).
        // Sem código, hash nem e-mail nos detalhes.
        await _audit.AddAsync(AuditLog.Create(
            user.Id, AuditAction.PasswordResetRequested, user.Id, null, ipAddress, DateTime.UtcNow), ct);

        // false = cooldown: sem token novo e sem e-mail; a resposta ao chamador é a mesma
        await _issuer.IssueAsync(user, UserTokenPurpose.PasswordReset, ct);
    }
}
