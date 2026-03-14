using PersonalFinance.Application.DTOs.Admin;
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

        public AssignRoleUseCase(
            IAdminUserRepository userRepository,
            IUserRoleRepository roleRepository,
            IUnitOfWork uow)
        {
            _userRepository = userRepository;
            _roleRepository = roleRepository;
            _uow = uow;
        }

        public async Task ExecuteAsync(AssignRoleDto dto, CancellationToken ct = default)
        {
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
            await _uow.CommitAsync(ct);
        }
    }
}
