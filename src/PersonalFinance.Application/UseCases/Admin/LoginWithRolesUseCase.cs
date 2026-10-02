using PersonalFinance.Application.Options;
using PersonalFinance.Domain.Exceptions;
using PersonalFinance.Domain.Interfaces.Repositories;
using PersonalFinance.Domain.Interfaces.Services;

namespace PersonalFinance.Application.UseCases.Admin
{
    /// <summary>
    /// Use case de login atualizado.
    /// Busca as roles do usuário e as inclui no token JWT para suportar
    /// [Authorize(Roles = "Admin")] nos controllers de configuração.
    /// </summary>
    public sealed class LoginWithRolesUseCase
    {
        private readonly IUserRepository _userRepository;
        private readonly IUserRoleRepository _roleRepository;
        private readonly IPasswordHasher _passwordHasher;
        private readonly ITokenService _tokenService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly LoginLockoutOptions _lockoutOptions;

        public LoginWithRolesUseCase(
            IUserRepository userRepository,
            IUserRoleRepository roleRepository,
            IPasswordHasher passwordHasher,
            ITokenService tokenService,
            IUnitOfWork unitOfWork,
            LoginLockoutOptions lockoutOptions)
        {
            _userRepository = userRepository;
            _roleRepository = roleRepository;
            _passwordHasher = passwordHasher;
            _tokenService = tokenService;
            _unitOfWork = unitOfWork;
            _lockoutOptions = lockoutOptions;
        }

        public async Task<DTOs.Auth.LoginResponseDto> ExecuteAsync(
            DTOs.Auth.LoginDto dto, CancellationToken ct = default)
        {
            const string InvalidCredentials = "Credenciais inválidas.";

            var user = await _userRepository.GetByEmailAsync(
                dto.Email.Trim().ToLowerInvariant(), ct);

            if (user is null)
                throw new DomainException(InvalidCredentials);

            if (!user.IsActive || user.IsDeleted)
                throw new DomainException("Usuário inativo.");

            var now = DateTime.UtcNow;

            // Conta bloqueada: falha antes do Verify, com mensagem genérica (sem enumeração)
            if (user.IsLockedOut(now))
                throw new DomainException(InvalidCredentials);

            if (!_passwordHasher.Verify(dto.Password, user.PasswordHash))
            {
                user.RegisterFailedLogin(
                    _lockoutOptions.MaxFailedAttempts,
                    TimeSpan.FromMinutes(_lockoutOptions.LockoutMinutes),
                    now);
                await _userRepository.UpdateAsync(user, ct);
                await _unitOfWork.CommitAsync(ct);
                throw new DomainException(InvalidCredentials);
            }

            // Sucesso: zera contador/bloqueio expirado somente se houver algo a limpar
            if (user.FailedLoginCount > 0 || user.LockedUntil is not null)
            {
                user.ResetFailedLogins();
                await _userRepository.UpdateAsync(user, ct);
                await _unitOfWork.CommitAsync(ct);
            }

            // Busca roles para incluir como claims no JWT
            var roles = await _roleRepository.GetRoleNamesByUserIdAsync(user.Id, ct);
            var token = _tokenService.Generate(user, roles);

            return new DTOs.Auth.LoginResponseDto(token, user.Name, user.Email);
        }
    }
}
