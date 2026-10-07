using PersonalFinance.Application.DTOs.Auth;
using PersonalFinance.Domain.Exceptions;
using PersonalFinance.Domain.Interfaces.Repositories;
using PersonalFinance.Domain.Interfaces.Services;

namespace PersonalFinance.Application.UseCases.Auth;

/// <summary>
/// Desativa o MFA exigindo senha E (código TOTP ou recovery code).
/// Limpa secret, recovery codes e flags.
/// </summary>
public sealed class DisableMfaUseCase(
    IUserRepository userRepository,
    IMfaRecoveryCodeRepository recoveryCodeRepository,
    ITotpService totpService,
    ISecretProtector secretProtector,
    IPasswordHasher passwordHasher,
    IUnitOfWork unitOfWork)
{
    public async Task ExecuteAsync(Guid userId, DisableMfaDto dto, CancellationToken ct = default)
    {
        var user = await userRepository.GetByIdAsync(userId, ct)
            ?? throw new KeyNotFoundException("Usuário não encontrado.");

        if (!user.MfaEnabled || string.IsNullOrWhiteSpace(user.MfaSecretEncrypted))
            throw new DomainException("O MFA não está ativo.");

        if (string.IsNullOrWhiteSpace(dto.Password) || string.IsNullOrWhiteSpace(dto.Code))
            throw new DomainException("Senha e código são obrigatórios.");

        if (!passwordHasher.Verify(dto.Password, user.PasswordHash))
            throw new DomainException("Senha ou código inválidos.");

        var code = dto.Code.Trim();
        var secret = secretProtector.Unprotect(user.MfaSecretEncrypted);

        // TOTP com anti-replay (step <= último é rejeitado); senão, tenta recovery code
        var step = totpService.ValidateCode(secret, code, DateTime.UtcNow);
        var accepted = step.HasValue && user.RegisterTotpStep(step.Value);

        if (!accepted)
        {
            var activeCodes = await recoveryCodeRepository.GetActiveByUserIdAsync(user.Id, ct);
            accepted = activeCodes.Any(c => passwordHasher.Verify(code, c.CodeHash));
        }

        if (!accepted)
            throw new DomainException("Senha ou código inválidos.");

        user.DisableMfa();
        await recoveryCodeRepository.RemoveAllByUserIdAsync(user.Id, ct);
        await userRepository.UpdateAsync(user, ct);
        await unitOfWork.CommitAsync(ct);
    }
}
