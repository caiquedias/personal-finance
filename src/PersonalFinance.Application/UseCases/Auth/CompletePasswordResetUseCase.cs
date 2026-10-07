using FluentValidation;
using PersonalFinance.Application.DTOs.Auth;
using PersonalFinance.Application.Interfaces;
using PersonalFinance.Application.Options;
using PersonalFinance.Domain.Entities.Auth;
using PersonalFinance.Domain.Enums;
using PersonalFinance.Domain.Exceptions;
using PersonalFinance.Domain.Interfaces.Repositories;
using PersonalFinance.Domain.Interfaces.Services;

namespace PersonalFinance.Application.UseCases.Auth;

/// <summary>
/// Conclui o reset de senha com e-mail + código. Qualquer falha (usuário/token inexistente, expirado,
/// usado, invalidado ou código errado) responde com a MESMA mensagem. Código e hash nunca são logados.
/// </summary>
public sealed class CompletePasswordResetUseCase
{
    private const string InvalidCode = "Código inválido ou expirado.";
    private const int MaxConcurrencyAttempts = 3;

    private readonly IUserRepository _users;
    private readonly IUserTokenRepository _tokens;
    private readonly IOneTimeCodeService _codes;
    private readonly IPasswordHasher _hasher;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _uow;
    private readonly UserTokenOptions _options;
    private readonly IValidator<CompletePasswordResetDto> _validator;

    public CompletePasswordResetUseCase(
        IUserRepository users,
        IUserTokenRepository tokens,
        IOneTimeCodeService codes,
        IPasswordHasher hasher,
        IAuditLogRepository audit,
        IUnitOfWork uow,
        UserTokenOptions options,
        IValidator<CompletePasswordResetDto> validator)
    {
        _users = users;
        _tokens = tokens;
        _codes = codes;
        _hasher = hasher;
        _audit = audit;
        _uow = uow;
        _options = options;
        _validator = validator;
    }

    public async Task ExecuteAsync(CompletePasswordResetDto dto, string? ipAddress, CancellationToken ct = default)
    {
        await _validator.ValidateAndThrowAsync(dto, ct);

        var email = dto.Email.Trim().ToLowerInvariant();
        string? newHash = null; // Argon2 é caro: calculado uma só vez, mesmo com retry

        // Concorrência otimista (rowversion do token): em conflito recarrega e reaplica
        for (var attempt = 1; attempt <= MaxConcurrencyAttempts; attempt++)
        {
            var user = await _users.GetByEmailAsync(email, ct);
            if (user is null || !user.IsActive || user.IsDeleted)
            {
                await RunDummyVerificationAsync(ct);
                throw new DomainException(InvalidCode);
            }

            var now = DateTime.UtcNow;
            var token = await _tokens.GetLatestAsync(user.Id, UserTokenPurpose.PasswordReset, ct);
            if (token is null || !token.IsUsable(now))
                throw new DomainException(InvalidCode);

            try
            {
                if (!_codes.Verify(token.Id, user.Id, UserTokenPurpose.PasswordReset, dto.Code, token.TokenHash))
                {
                    // A N-ésima falha invalida o token (código certo depois é recusado)
                    token.RegisterFailedAttempt(_options.MaxAttempts);
                    await _tokens.UpdateAsync(token, ct);
                    await _uow.CommitAsync(ct);
                    throw new DomainException(InvalidCode);
                }

                newHash ??= _hasher.Hash(dto.NewPassword);
                user.UpdatePasswordHash(newHash);
                user.RotateSecurityStamp(); // invalida sessões/JWTs anteriores
                user.ResetFailedLogins();
                user.ConfirmEmail(now);     // receber o código no e-mail prova a posse
                token.MarkUsed(now);

                await _users.UpdateAsync(user, ct);
                await _tokens.UpdateAsync(token, ct);

                // Ator = alvo = o próprio usuário; sem código, senha nem hash nos detalhes
                await _audit.AddAsync(AuditLog.Create(
                    user.Id, AuditAction.PasswordResetCompleted, user.Id, null, ipAddress, now), ct);

                await _uow.CommitAsync(ct);
                return;
            }
            catch (ConcurrencyConflictException)
            {
                if (attempt == MaxConcurrencyAttempts)
                    throw new DomainException(InvalidCode);
            }
        }

        // Inalcançável: o laço sempre retorna ou lança
        throw new DomainException(InvalidCode);
    }

    /// <summary>
    /// Equalização APROXIMADA de timing para usuário inexistente/inativo/removido: HMAC + verificação e uma
    /// leitura de token com valores fixos, sem persistir nada. Não iguala o custo do commit do contador
    /// de tentativas; o resíduo é mitigado pelo rate limit por IP e pela resposta idêntica.
    /// </summary>
    private async Task RunDummyVerificationAsync(CancellationToken ct)
    {
        var purpose = UserTokenPurpose.PasswordReset;
        var dummyHash = _codes.ComputeHash(Guid.Empty, Guid.Empty, purpose, "000000");
        _codes.Verify(Guid.Empty, Guid.Empty, purpose, "000000", dummyHash);
        await _tokens.GetLatestAsync(Guid.Empty, purpose, ct);
    }
}
