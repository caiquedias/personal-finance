using FluentValidation;
using PersonalFinance.Application.DTOs.Admin;
using PersonalFinance.Application.Services.Audit;
using PersonalFinance.Domain.Entities.Auth;
using PersonalFinance.Domain.Enums;
using PersonalFinance.Domain.Exceptions;
using PersonalFinance.Domain.Interfaces.Repositories;

namespace PersonalFinance.Application.UseCases.Admin
{
    /// <summary>Atribui uma role a um usuário.</summary>
    public sealed class AssignRoleUseCase
    {
        private readonly IAdminUserRepository _userRepository;
        private readonly IUserRoleRepository _roleRepository;
        private readonly IUnitOfWork _uow;
        private readonly IAuditLogRepository _auditRepository;
        private readonly IValidator<AssignRoleDto> _validator;

        public AssignRoleUseCase(
            IAdminUserRepository userRepository,
            IUserRoleRepository roleRepository,
            IUnitOfWork uow,
            IAuditLogRepository auditRepository,
            IValidator<AssignRoleDto> validator)
        {
            _userRepository = userRepository;
            _roleRepository = roleRepository;
            _uow = uow;
            _auditRepository = auditRepository;
            _validator = validator;
        }

        public async Task ExecuteAsync(AssignRoleDto dto, Guid requestingAdminId, string? ipAddress,
            CancellationToken ct = default)
        {
            await _validator.ValidateAndThrowAsync(dto, ct);

            var user = await _userRepository.GetByIdAsync(dto.UserId, ct)
                ?? throw new KeyNotFoundException("Usuário não encontrado.");

            if (!user.IsActive)
                throw new DomainException("Não é possível atribuir roles a um usuário inativo.");

            var alreadyHas = await _roleRepository.UserHasRoleAsync(dto.UserId, dto.RoleId, ct);
            if (alreadyHas)
                throw new DomainException("O usuário já possui esta role.");

            var userRole = new Domain.Entities.Auth.UserRole
            {
                UserId = dto.UserId,
                RoleId = dto.RoleId,
                AssignedAt = DateTime.UtcNow
            };

            await _roleRepository.AssignAsync(userRole, ct);
            // Auditoria na mesma transação; apenas ids (LGPD)
            var details = AuditDetailsSerializer.Serialize(
                new Dictionary<string, object?> { ["roleId"] = dto.RoleId });
            await _auditRepository.AddAsync(AuditLog.Create(
                requestingAdminId, AuditAction.RoleAssigned, dto.UserId, details, ipAddress, DateTime.UtcNow), ct);

            await _uow.CommitAsync(ct);
        }
    }
}
