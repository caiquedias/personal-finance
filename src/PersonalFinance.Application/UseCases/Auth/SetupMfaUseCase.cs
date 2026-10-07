using PersonalFinance.Application.DTOs.Auth;
using PersonalFinance.Domain.Exceptions;
using PersonalFinance.Domain.Interfaces.Repositories;
using PersonalFinance.Domain.Interfaces.Services;

namespace PersonalFinance.Application.UseCases.Auth;

/// <summary>
/// Inicia a configuração do MFA: gera o secret TOTP, guarda CIFRADO como pendente (MfaEnabled=false)
/// e devolve o secret em claro + URI otpauth. Com MFA já ativo, não sobrescreve o secret.
/// </summary>
public sealed class SetupMfaUseCase(
    IUserRepository userRepository,
    ITotpService totpService,
    ISecretProtector secretProtector,
    IUnitOfWork unitOfWork)
{
    public async Task<MfaSetupResponseDto> ExecuteAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await userRepository.GetByIdAsync(userId, ct)
            ?? throw new KeyNotFoundException("Usuário não encontrado.");

        if (user.MfaEnabled)
            throw new DomainException("O MFA já está ativo. Desative-o antes de reconfigurar.");

        var secret = totpService.GenerateSecret();
        user.SetPendingMfaSecret(secretProtector.Protect(secret));

        await userRepository.UpdateAsync(user, ct);
        await unitOfWork.CommitAsync(ct);

        return new MfaSetupResponseDto(secret, totpService.BuildOtpAuthUri(secret, user.Email));
    }
}
