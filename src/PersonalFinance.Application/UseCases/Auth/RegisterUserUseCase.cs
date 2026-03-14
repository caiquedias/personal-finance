using PersonalFinance.Application.DTOs.Auth;
using PersonalFinance.Domain.Entities.Auth;
using PersonalFinance.Domain.Exceptions;
using PersonalFinance.Domain.Interfaces.Repositories;
using PersonalFinance.Domain.Interfaces.Services;

namespace PersonalFinance.Application.UseCases.Auth
{
    /// <summary>
    /// Registra um novo usuário no sistema.
    /// Valida unicidade de e-mail, gera hash Argon2id e persiste via repositório.
    /// </summary>
    public sealed class RegisterUserUseCase
    {
        private readonly IUserRepository _userRepository;
        private readonly IPasswordHasher _passwordHasher;
        private readonly IUnitOfWork _unitOfWork;

        public RegisterUserUseCase(
            IUserRepository userRepository,
            IPasswordHasher passwordHasher,
            IUnitOfWork unitOfWork)
        {
            _userRepository = userRepository;
            _passwordHasher = passwordHasher;
            _unitOfWork = unitOfWork;
        }

        public async Task<UserResponseDto> ExecuteAsync(
            RegisterUserDto dto,
            CancellationToken ct = default)
        {
            // Valida campos antes de qualquer acesso ao banco
            if (string.IsNullOrWhiteSpace(dto.Name))
                throw new DomainException("O nome do usuário é obrigatório.");

            if (string.IsNullOrWhiteSpace(dto.Email))
                throw new DomainException("O e-mail do usuário é obrigatório.");

            if (string.IsNullOrWhiteSpace(dto.Password))
                throw new DomainException("A senha é obrigatória.");

            // Verifica unicidade de e-mail (normalizado para lowercase)
            var emailNormalized = dto.Email.Trim().ToLowerInvariant();
            var exists = await _userRepository.ExistsByEmailAsync(emailNormalized, ct);

            if (exists)
                throw new DomainException(
                    $"O e-mail '{emailNormalized}' já está em uso.");

            // Gera hash Argon2id antes de criar a entidade
            var passwordHash = _passwordHasher.Hash(dto.Password);

            var user = User.Create(dto.Name, dto.Email, passwordHash);

            await _userRepository.AddAsync(user, ct);
            await _unitOfWork.CommitAsync(ct);

            return ToDto(user);
        }

        private static UserResponseDto ToDto(User user) =>
            new(user.Id, user.Name, user.Email, user.IsActive);
    }
}
