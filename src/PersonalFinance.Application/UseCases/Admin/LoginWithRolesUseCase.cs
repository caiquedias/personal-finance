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

        public LoginWithRolesUseCase(
            IUserRepository userRepository,
            IUserRoleRepository roleRepository,
            IPasswordHasher passwordHasher,
            ITokenService tokenService)
        {
            _userRepository = userRepository;
            _roleRepository = roleRepository;
            _passwordHasher = passwordHasher;
            _tokenService = tokenService;
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

            if (!_passwordHasher.Verify(dto.Password, user.PasswordHash))
                throw new DomainException(InvalidCredentials);

            // Busca roles para incluir como claims no JWT
            var roles = await _roleRepository.GetRoleNamesByUserIdAsync(user.Id, ct);
            var token = _tokenService.Generate(user, roles);

            return new DTOs.Auth.LoginResponseDto(token, user.Name, user.Email);
        }
    }
}
