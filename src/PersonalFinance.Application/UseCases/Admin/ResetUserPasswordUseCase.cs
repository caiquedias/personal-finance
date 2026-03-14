using PersonalFinance.Application.DTOs.Admin;
using PersonalFinance.Domain.Exceptions;
using PersonalFinance.Domain.Interfaces.Repositories;
using PersonalFinance.Domain.Interfaces.Services;

namespace PersonalFinance.Application.UseCases.Admin
{
    /// <summary>
    /// Reseta a senha de um usuário.
    /// Admin não pode redefinir a própria senha por aqui — deve usar o endpoint padrão de perfil.
    /// </summary>
    public sealed class ResetUserPasswordUseCase
    {
        private readonly IAdminUserRepository _userRepository;
        private readonly IPasswordHasher _hasher;
        private readonly IUnitOfWork _uow;

        public ResetUserPasswordUseCase(
            IAdminUserRepository userRepository,
            IPasswordHasher hasher,
            IUnitOfWork uow)
        {
            _userRepository = userRepository;
            _hasher = hasher;
            _uow = uow;
        }

        public async Task ExecuteAsync(
            ResetPasswordDto dto, Guid requestingAdminId,
            CancellationToken ct = default)
        {
            if (dto.UserId == requestingAdminId)
                throw new DomainException("Use o endpoint de perfil para alterar sua própria senha.");

            if (string.IsNullOrWhiteSpace(dto.NewPassword) || dto.NewPassword.Length < 8)
                throw new DomainException("A nova senha deve ter no mínimo 8 caracteres.");

            var user = await _userRepository.GetByIdAsync(dto.UserId, ct)
                ?? throw new KeyNotFoundException("Usuário não encontrado.");

            var newHash = _hasher.Hash(dto.NewPassword);
            user.UpdatePasswordHash(newHash);

            await _uow.CommitAsync(ct);
        }
    }
}
