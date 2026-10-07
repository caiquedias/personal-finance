using PersonalFinance.Application.DTOs.Admin;
using PersonalFinance.Application.Services.Audit;
using PersonalFinance.Domain.Entities.Auth;
using PersonalFinance.Domain.Enums;
using PersonalFinance.Domain.Exceptions;
using PersonalFinance.Domain.Interfaces.Repositories;

namespace PersonalFinance.Application.UseCases.Admin
{
    /// <summary>
    /// Remove uma role de um usuário.
    /// Admin não pode remover sua própria role Admin.
    /// </summary>
    public sealed class RemoveRoleUseCase
    {
        private readonly IUserRoleRepository _roleRepository;
        private readonly IUnitOfWork _uow;
        private readonly IAuditLogRepository _auditRepository;

        public RemoveRoleUseCase(
            IUserRoleRepository roleRepository, IUnitOfWork uow, IAuditLogRepository auditRepository)
        {
            _roleRepository = roleRepository;
            _uow = uow;
            _auditRepository = auditRepository;
        }

        public async Task ExecuteAsync(
            RemoveRoleDto dto, Guid requestingAdminId, string? ipAddress,
            CancellationToken ct = default)
        {
            // Impede que o admin remova a própria role Admin (RoleId = 1)
            if (dto.UserId == requestingAdminId && dto.RoleId == 1)
                throw new DomainException("Um administrador não pode remover a própria role Admin.");

            var hasRole = await _roleRepository.UserHasRoleAsync(dto.UserId, dto.RoleId, ct);
            if (!hasRole)
                throw new DomainException("O usuário não possui esta role.");

            await _roleRepository.RemoveAsync(dto.UserId, dto.RoleId, ct);
            // Auditoria na mesma transação; apenas ids (LGPD)
            var details = AuditDetailsSerializer.Serialize(
                new Dictionary<string, object?> { ["roleId"] = dto.RoleId });
            await _auditRepository.AddAsync(AuditLog.Create(
                requestingAdminId, AuditAction.RoleRemoved, dto.UserId, details, ipAddress, DateTime.UtcNow), ct);

            await _uow.CommitAsync(ct);
        }
    }
}
