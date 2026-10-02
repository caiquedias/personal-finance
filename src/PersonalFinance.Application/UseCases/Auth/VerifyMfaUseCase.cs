using PersonalFinance.Application.DTOs.Auth;
using PersonalFinance.Application.Options;
using PersonalFinance.Domain.Interfaces.Repositories;
using PersonalFinance.Domain.Interfaces.Services;

namespace PersonalFinance.Application.UseCases.Auth;

/// <summary>
/// 2º fator do login (Task 2 da #393) — implementação pendente.
/// Esqueleto necessário para compilar a solução enquanto a Task 1 (gestão de MFA) é entregue.
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
    public Task<LoginResponseDto> ExecuteAsync(Guid userId, VerifyMfaDto dto, string ipAddress, CancellationToken ct = default)
        => throw new NotImplementedException("Task 2 da #393.");
}
