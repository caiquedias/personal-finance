using FluentValidation;
using PersonalFinance.Application.DTOs.Auth;
using PersonalFinance.Application.Interfaces;
using PersonalFinance.Application.Options;
using PersonalFinance.Domain.Enums;
using PersonalFinance.Domain.Exceptions;
using PersonalFinance.Domain.Interfaces.Repositories;

namespace PersonalFinance.Application.UseCases.Auth;

/// <summary>
/// Confirma o e-mail com e-mail + código. Qualquer falha (usuário/token inexistente, expirado, usado,
/// invalidado ou código errado) responde com a MESMA mensagem. Código e hash nunca são logados.
/// </summary>
public sealed class ConfirmEmailUseCase
{
    private const string InvalidCode = "Código inválido ou expirado.";
    private const int MaxConcurrencyAttempts = 3;

    private readonly IUserRepository _users;
    private readonly IUserTokenRepository _tokens;
    private readonly IOneTimeCodeService _codes;
    private readonly IUnitOfWork _uow;
    private readonly UserTokenOptions _options;
    private readonly IValidator<ConfirmEmailDto> _validator;

    public ConfirmEmailUseCase(
        IUserRepository users,
        IUserTokenRepository tokens,
        IOneTimeCodeService codes,
        IUnitOfWork uow,
        UserTokenOptions options,
        IValidator<ConfirmEmailDto> validator)
    {
        _users = users;
        _tokens = tokens;
        _codes = codes;
        _uow = uow;
        _options = options;
        _validator = validator;
    }

    public async Task ExecuteAsync(ConfirmEmailDto dto, CancellationToken ct = default)
    {
        await _validator.ValidateAndThrowAsync(dto, ct);

        var email = dto.Email.Trim().ToLowerInvariant();

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
            var token = await _tokens.GetLatestAsync(user.Id, UserTokenPurpose.EmailVerification, ct);
            if (token is null || !token.IsUsable(now))
                throw new DomainException(InvalidCode);

            try
            {
                if (!_codes.Verify(token.Id, user.Id, UserTokenPurpose.EmailVerification, dto.Code, token.TokenHash))
                {
                    // A N-ésima falha invalida o token (código certo depois é recusado)
                    token.RegisterFailedAttempt(_options.MaxAttempts);
                    await _tokens.UpdateAsync(token, ct);
                    await _uow.CommitAsync(ct);
                    throw new DomainException(InvalidCode);
                }

                user.ConfirmEmail(now);
                token.MarkUsed(now);

                await _users.UpdateAsync(user, ct);
                await _tokens.UpdateAsync(token, ct);
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
        var purpose = UserTokenPurpose.EmailVerification;
        var dummyHash = _codes.ComputeHash(Guid.Empty, Guid.Empty, purpose, "000000");
        _codes.Verify(Guid.Empty, Guid.Empty, purpose, "000000", dummyHash);
        await _tokens.GetLatestAsync(Guid.Empty, purpose, ct);
    }
}
