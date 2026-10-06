using FluentValidation;
using PersonalFinance.Application.DTOs.Auth;
using PersonalFinance.Application.Services.Auth;
using PersonalFinance.Domain.Entities.Auth;
using PersonalFinance.Domain.Enums;
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
        private readonly IValidator<RegisterUserDto> _validator;
        private readonly UserTokenIssuer _tokenIssuer;

        public RegisterUserUseCase(
            IUserRepository userRepository,
            IPasswordHasher passwordHasher,
            IUnitOfWork unitOfWork,
            IValidator<RegisterUserDto> validator,
            UserTokenIssuer tokenIssuer)
        {
            _userRepository = userRepository;
            _passwordHasher = passwordHasher;
            _unitOfWork = unitOfWork;
            _validator = validator;
            _tokenIssuer = tokenIssuer;
        }

        public async Task ExecuteAsync(
            RegisterUserDto dto,
            CancellationToken ct = default)
        {
            // Valida o DTO antes de qualquer acesso ao banco
            await _validator.ValidateAndThrowAsync(dto, ct);

            // Defesa em profundidade (mantida para chamadas sem validator efetivo)
            if (string.IsNullOrWhiteSpace(dto.Name))
                throw new DomainException("O nome do usuário é obrigatório.");

            if (string.IsNullOrWhiteSpace(dto.Email))
                throw new DomainException("O e-mail do usuário é obrigatório.");

            if (string.IsNullOrWhiteSpace(dto.Password))
                throw new DomainException("A senha é obrigatória.");

            // Verifica unicidade de e-mail (normalizado para lowercase)
            var emailNormalized = dto.Email.Trim().ToLowerInvariant();
            var exists = await _userRepository.ExistsByEmailAsync(emailNormalized, ct);

            // Gera hash Argon2id antes de qualquer desvio (também no duplicado: equaliza o tempo)
            var passwordHash = _passwordHasher.Hash(dto.Password);

            // Anti-enumeração: duplicado responde igual ao sucesso, sem efeitos
            if (exists)
                return;

            var user = User.Create(dto.Name, dto.Email, passwordHash);

            await _userRepository.AddAsync(user, ct);

            // O issuer commita (usuário + token na mesma unidade de trabalho) e só então enfileira o e-mail
            await _tokenIssuer.IssueAsync(user, UserTokenPurpose.EmailVerification, ct);
        }
    }
}
