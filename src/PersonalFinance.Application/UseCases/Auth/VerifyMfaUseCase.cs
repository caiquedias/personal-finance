using PersonalFinance.Application.DTOs.Auth;
using PersonalFinance.Application.Options;
using PersonalFinance.Domain.Entities.Auth;
using PersonalFinance.Domain.Exceptions;
using PersonalFinance.Domain.Interfaces.Repositories;
using PersonalFinance.Domain.Interfaces.Services;

namespace PersonalFinance.Application.UseCases.Auth;

/// <summary>
/// 2º fator do login: valida o código TOTP (com anti-replay por time step) ou um recovery code de uso único.
/// Falhas contam no lockout global (User) e por par (conta, IP) — mesmas regras do login (#391).
/// Sucesso emite o token completo e zera os contadores.
/// </summary>
public sealed class VerifyMfaUseCase(
    IUserRepository userRepository,
    IUserRoleRepository roleRepository,
    ILoginThrottleRepository throttleRepository,
    IMfaRecoveryCodeRepository recoveryCodeRepository,
    ITotpService totpService,
    ISecretProtector secretProtector,
    IPasswordHasher passwordHasher,
    ITokenService tokenService,
    IUnitOfWork unitOfWork,
    LoginLockoutOptions lockoutOptions)
{
    // Mensagem única para qualquer rejeição (não revela se a conta existe, está bloqueada ou o código errou)
    private const string InvalidCode = "Código inválido.";

    public async Task<LoginResponseDto> ExecuteAsync(
        Guid userId, VerifyMfaDto dto, string ipAddress, CancellationToken ct = default)
    {
        var user = await userRepository.GetByIdAsync(userId, ct);

        if (user is null || !user.IsActive || user.IsDeleted
            || !user.MfaEnabled || string.IsNullOrWhiteSpace(user.MfaSecretEncrypted))
            throw new DomainException(InvalidCode);

        var now = DateTime.UtcNow;
        var window = TimeSpan.FromMinutes(lockoutOptions.LockoutMinutes);

        // Bloqueios: rejeita sem validar o código (nem consumir recovery code)
        if (user.IsLockedOut(now))
            throw new DomainException(InvalidCode);

        var throttle = await throttleRepository.GetAsync(user.Id, ipAddress, ct);
        if (throttle is not null && throttle.IsLockedOut(now))
            throw new DomainException(InvalidCode);

        var code = (dto.Code ?? string.Empty).Trim();

        try
        {
            if (!await IsSecondFactorValidAsync(user, code, now, ct))
            {
                await RegisterFailureAsync(user, throttle, ipAddress, window, now, ct);
                await unitOfWork.CommitAsync(ct);
                throw new DomainException(InvalidCode);
            }

            // Sucesso: zera contadores no mesmo commit que grava o step/recovery usado
            if (user.FailedLoginCount > 0 || user.LockedUntil is not null)
                user.ResetFailedLogins();

            await userRepository.UpdateAsync(user, ct);

            if (throttle is not null)
                await throttleRepository.RemoveAsync(throttle, ct);

            await unitOfWork.CommitAsync(ct);
        }
        catch (ConcurrencyConflictException)
        {
            // Conflito (ex.: mesmo código usado em paralelo): rejeita sem reprocessar
            throw new DomainException(InvalidCode);
        }

        var roles = await roleRepository.GetRoleNamesByUserIdAsync(user.Id, ct);
        var token = tokenService.Generate(user, roles);

        return new LoginResponseDto(token, user.Name, user.Email);
    }

    /// <summary>TOTP primeiro (anti-replay); só tenta recovery code se o código não for um TOTP válido.</summary>
    private async Task<bool> IsSecondFactorValidAsync(User user, string code, DateTime now, CancellationToken ct)
    {
        var secret = secretProtector.Unprotect(user.MfaSecretEncrypted!);
        var step = totpService.ValidateCode(secret, code, now);

        if (step.HasValue)
            return user.RegisterTotpStep(step.Value);

        var activeCodes = await recoveryCodeRepository.GetActiveByUserIdAsync(user.Id, ct);
        foreach (var recovery in activeCodes)
        {
            if (!passwordHasher.Verify(code, recovery.CodeHash))
                continue;

            recovery.MarkUsed(now);
            return true;
        }

        return false;
    }

    private async Task RegisterFailureAsync(
        User user, LoginThrottle? throttle, string ipAddress, TimeSpan window, DateTime now, CancellationToken ct)
    {
        // Contador global usa o teto mais alto (cobre ataque distribuído)
        user.RegisterFailedLogin(lockoutOptions.GlobalMaxFailedAttempts, window, now);

        if (throttle is not null)
        {
            throttle.RegisterFailure(lockoutOptions.MaxFailedAttempts, window, now);
            await throttleRepository.UpdateAsync(throttle, ct);
        }
        else
        {
            var created = LoginThrottle.Create(user.Id, ipAddress, now);
            created.RegisterFailure(lockoutOptions.MaxFailedAttempts, window, now);
            // false = tabela cheia de bloqueios ativos: segue só com o teto global (fail-open)
            await throttleRepository.TryAddAsync(
                created, now, window,
                lockoutOptions.ThrottleMaxRows,
                lockoutOptions.ThrottleCleanupBatchSize, ct);
        }

        await userRepository.UpdateAsync(user, ct);
    }
}
