using PersonalFinance.Application.DTOs.Admin;
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

        public RemoveRoleUseCase(IUserRoleRepository roleRepository, IUnitOfWork uow)
        {
            _roleRepository = roleRepository;
            _uow = uow;
        }

        public async Task ExecuteAsync(
            RemoveRoleDto dto, Guid requestingAdminId,
            CancellationToken ct = default)
        {
            // Impede que o admin remova a própria role Admin (RoleId = 1)
            if (dto.UserId == requestingAdminId && dto.RoleId == 1)
                throw new DomainException("Um administrador não pode remover a própria role Admin.");

            var hasRole = await _roleRepository.UserHasRoleAsync(dto.UserId, dto.RoleId, ct);
            if (!hasRole)
                throw new DomainException("O usuário não possui esta role.");

            await _roleRepository.RemoveAsync(dto.UserId, dto.RoleId, ct);
            await _uow.CommitAsync(ct);
        }
    }
}
