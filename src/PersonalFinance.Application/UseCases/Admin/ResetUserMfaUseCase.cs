using PersonalFinance.Domain.Entities.Auth;
using PersonalFinance.Domain.Enums;
using PersonalFinance.Domain.Exceptions;
using PersonalFinance.Domain.Interfaces.Repositories;

namespace PersonalFinance.Application.UseCases.Admin
{
    /// <summary>
    /// Reseta o MFA de um usuário (desativa, limpa secret e remove recovery codes).
    /// Admin não pode resetar o próprio MFA por aqui — deve usar o endpoint de desativação.
    /// </summary>
    public sealed class ResetUserMfaUseCase
    {
        private readonly IAdminUserRepository _userRepository;
        private readonly IMfaRecoveryCodeRepository _recoveryCodeRepository;
        private readonly IUnitOfWork _uow;
        private readonly IAuditLogRepository _auditRepository;

        public ResetUserMfaUseCase(
            IAdminUserRepository userRepository,
            IMfaRecoveryCodeRepository recoveryCodeRepository,
            IUnitOfWork uow,
            IAuditLogRepository auditRepository)
        {
            _userRepository = userRepository;
            _recoveryCodeRepository = recoveryCodeRepository;
            _uow = uow;
            _auditRepository = auditRepository;
        }

        public async Task ExecuteAsync(
            Guid userId, Guid requestingAdminId, string? ipAddress,
            CancellationToken ct = default)
        {
            if (userId == requestingAdminId)
                throw new DomainException("Use o endpoint de MFA do perfil para desativar seu próprio MFA.");

            var user = await _userRepository.GetByIdAsync(userId, ct)
                ?? throw new KeyNotFoundException("Usuário não encontrado.");

            // Secret pendente (MFA não concluído) também é resetado
            if (!user.MfaEnabled && user.MfaSecretEncrypted is null)
                throw new DomainException("O MFA não está ativo.");

            user.DisableMfa();
            user.RotateSecurityStamp(); // invalida sessões ativas do alvo (#489)
            await _recoveryCodeRepository.RemoveAllByUserIdAsync(user.Id, ct);
            // Auditoria na mesma transação; sem secret nem recovery codes (LGPD)
            await _auditRepository.AddAsync(AuditLog.Create(
                requestingAdminId, AuditAction.MfaReset, userId, null, ipAddress, DateTime.UtcNow), ct);
            await _uow.CommitAsync(ct);
        }
    }
}
