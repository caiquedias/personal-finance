using System.Security.Cryptography;
using PersonalFinance.Application.DTOs.Auth;
using PersonalFinance.Domain.Entities.Auth;
using PersonalFinance.Domain.Exceptions;
using PersonalFinance.Domain.Interfaces.Repositories;
using PersonalFinance.Domain.Interfaces.Services;

namespace PersonalFinance.Application.UseCases.Auth;

/// <summary>
/// Conclui a ativação do MFA: valida o 1º código TOTP contra o secret pendente, ativa o MFA
/// e gera os recovery codes (exibidos uma única vez; só o hash é persistido).
/// Regerar invalida os anteriores. O step do código fica registrado (anti-replay).
/// </summary>
public sealed class EnableMfaUseCase(
    IUserRepository userRepository,
    IMfaRecoveryCodeRepository recoveryCodeRepository,
    ITotpService totpService,
    ISecretProtector secretProtector,
    IPasswordHasher passwordHasher,
    IUnitOfWork unitOfWork)
{
    private const int RecoveryCodeCount = 10;
    private const int RecoveryCodeLength = 10;

    // Sem caracteres ambíguos (0/O, 1/I/L) para facilitar a digitação
    private const string RecoveryAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    public async Task<EnableMfaResponseDto> ExecuteAsync(Guid userId, EnableMfaDto dto, CancellationToken ct = default)
    {
        var user = await userRepository.GetByIdAsync(userId, ct)
            ?? throw new KeyNotFoundException("Usuário não encontrado.");

        if (user.MfaEnabled)
            throw new DomainException("O MFA já está ativo.");

        if (string.IsNullOrWhiteSpace(user.MfaSecretEncrypted))
            throw new DomainException("Configure o MFA antes de ativá-lo.");

        var secret = secretProtector.Unprotect(user.MfaSecretEncrypted);
        var step = totpService.ValidateCode(secret, dto.Code?.Trim() ?? string.Empty, DateTime.UtcNow);
        if (step is null || !user.RegisterTotpStep(step.Value))
            throw new DomainException("Código inválido.");

        user.EnableMfa(DateTime.UtcNow);

        var plainCodes = GenerateRecoveryCodes();
        var entities = plainCodes.Select(c => MfaRecoveryCode.Create(user.Id, passwordHasher.Hash(c))).ToList();

        // Regerar invalida os códigos anteriores
        await recoveryCodeRepository.RemoveAllByUserIdAsync(user.Id, ct);
        await recoveryCodeRepository.AddRangeAsync(entities, ct);
        await userRepository.UpdateAsync(user, ct);
        await unitOfWork.CommitAsync(ct);

        return new EnableMfaResponseDto(plainCodes);
    }

    /// <summary>Gera códigos distintos com CSPRNG (sem viés de módulo).</summary>
    private static List<string> GenerateRecoveryCodes()
    {
        var codes = new HashSet<string>();
        while (codes.Count < RecoveryCodeCount)
        {
            codes.Add(string.Create(RecoveryCodeLength, 0, static (span, _) =>
            {
                for (var i = 0; i < span.Length; i++)
                    span[i] = RecoveryAlphabet[RandomNumberGenerator.GetInt32(RecoveryAlphabet.Length)];
            }));
        }
        return codes.ToList();
    }
}
