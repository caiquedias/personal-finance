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

        public ToggleUserActiveUseCase(IAdminUserRepository userRepository, IUnitOfWork uow)
        {
            _userRepository = userRepository;
            _uow = uow;
        }

        public async Task ExecuteAsync(
            Guid targetUserId, Guid requestingAdminId,
            CancellationToken ct = default)
        {
            if (targetUserId == requestingAdminId)
                throw new DomainException("Um administrador não pode desativar a si próprio.");

            var user = await _userRepository.GetByIdAsync(targetUserId, ct)
                ?? throw new KeyNotFoundException("Usuário não encontrado.");

            if (user.IsActive)
                user.SoftDelete();
            else
            {
                // Reativar — como SoftDelete seta DeletedAt, precisamos de um método na entidade
                // A entidade User herda EntityBase — vamos chamar Reactivate
                user.Reactivate();
            }

            await _uow.CommitAsync(ct);
        }
    }
}
