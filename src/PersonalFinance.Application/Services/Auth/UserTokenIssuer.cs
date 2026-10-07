using PersonalFinance.Application.Interfaces;
using PersonalFinance.Application.Options;
using PersonalFinance.Domain.Entities.Auth;
using PersonalFinance.Domain.Enums;
using PersonalFinance.Domain.Exceptions;
using PersonalFinance.Domain.Interfaces.Repositories;

namespace PersonalFinance.Application.Services.Auth;

/// <summary>
/// Emite códigos de uso único (reset de senha / verificação de e-mail): respeita o cooldown por conta,
/// apaga os anteriores, persiste só o HMAC do código e enfileira o e-mail após o commit.
/// Código e hash nunca são logados.
/// </summary>
public sealed class UserTokenIssuer
{
    private readonly IUserTokenRepository _tokens;
    private readonly IOneTimeCodeService _codes;
    private readonly IEmailQueue _queue;
    private readonly IUnitOfWork _uow;
    private readonly UserTokenOptions _options;
    private readonly AuthEmailComposer _composer;

    public UserTokenIssuer(
        IUserTokenRepository tokens,
        IOneTimeCodeService codes,
        IEmailQueue queue,
        IUnitOfWork uow,
        UserTokenOptions options,
        AuthEmailComposer composer)
    {
        _tokens = tokens;
        _codes = codes;
        _queue = queue;
        _uow = uow;
        _options = options;
        _composer = composer;
    }

    /// <summary>
    /// Emite um novo código e faz o commit. Retorna false (sem efeitos) se ainda estiver no cooldown.
    /// </summary>
    public async Task<bool> IssueAsync(User user, UserTokenPurpose purpose, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        var latest = await _tokens.GetLatestAsync(user.Id, purpose, ct);
        if (latest is not null && now - latest.CreatedAt < TimeSpan.FromSeconds(_options.ResendCooldownSeconds))
            return false;

        await _tokens.RemoveAllAsync(user.Id, purpose, ct);

        var token = UserToken.Create(user.Id, purpose, now, TimeSpan.FromMinutes(_options.CodeTtlMinutes));
        var code = _codes.GenerateCode();
        token.SetTokenHash(_codes.ComputeHash(token.Id, user.Id, purpose, code));

        await _tokens.AddAsync(token, ct);

        try
        {
            await _uow.CommitAsync(ct);
        }
        catch (ConcurrencyConflictException)
        {
            // Corrida (DELETE concorrente do token ou e-mail duplicado): sem enfileirar nem propagar,
            // para não criar oráculo de enumeração (409 só para contas existentes)
            return false;
        }

        // Só após persistir: nunca enviar um código que não foi gravado
        var message = purpose == UserTokenPurpose.PasswordReset
            ? _composer.ComposePasswordReset(user.Email, code, _options.CodeTtlMinutes)
            : _composer.ComposeEmailVerification(user.Email, code, _options.CodeTtlMinutes);
        _queue.TryEnqueue(message);

        return true;
    }
}
