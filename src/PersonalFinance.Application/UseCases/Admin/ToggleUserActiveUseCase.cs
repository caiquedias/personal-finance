using PersonalFinance.Domain.Entities.Auth;
using PersonalFinance.Domain.Enums;
using PersonalFinance.Domain.Exceptions;
using PersonalFinance.Domain.Interfaces.Repositories;

namespace PersonalFinance.Application.UseCases.Admin
{
    /// <summary>
    /// Ativa ou desativa um usuário.
    /// Admin não pode desativar a si próprio.
    /// </summary>
    public sealed class ToggleUserActiveUseCase
    {
        private readonly IAdminUserRepository _userRepository;
        private readonly IUnitOfWork _uow;
        private readonly IAuditLogRepository _auditRepository;

        public ToggleUserActiveUseCase(
            IAdminUserRepository userRepository, IUnitOfWork uow, IAuditLogRepository auditRepository)
        {
            _userRepository = userRepository;
            _uow = uow;
            _auditRepository = auditRepository;
        }

        public async Task ExecuteAsync(
            Guid targetUserId, Guid requestingAdminId, string? ipAddress,
            CancellationToken ct = default)
        {
            if (targetUserId == requestingAdminId)
                throw new DomainException("Um administrador não pode desativar a si próprio.");

            var user = await _userRepository.GetByIdAsync(targetUserId, ct)
                ?? throw new KeyNotFoundException("Usuário não encontrado.");

            var action = user.IsActive ? AuditAction.UserDeactivated : AuditAction.UserActivated;

            if (user.IsActive)
            {
                user.SoftDelete();
                user.RotateSecurityStamp(); // desativação invalida sessões ativas (#489)
            }
            else
            {
                // Reativar — como SoftDelete seta DeletedAt, precisamos de um método na entidade
                // A entidade User herda EntityBase — vamos chamar Reactivate
                user.Reactivate();
            }

            await _auditRepository.AddAsync(AuditLog.Create(
                requestingAdminId, action, targetUserId, null, ipAddress, DateTime.UtcNow), ct);

            await _uow.CommitAsync(ct);
        }
    }
}
