using PersonalFinance.Application.Options;
using PersonalFinance.Domain.Entities.Auth;
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
        private readonly ILoginThrottleRepository _throttleRepository;
        private readonly IPasswordHasher _passwordHasher;
        private readonly ITokenService _tokenService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly LoginLockoutOptions _lockoutOptions;

        public LoginWithRolesUseCase(
            IUserRepository userRepository,
            IUserRoleRepository roleRepository,
            ILoginThrottleRepository throttleRepository,
            IPasswordHasher passwordHasher,
            ITokenService tokenService,
            IUnitOfWork unitOfWork,
            LoginLockoutOptions lockoutOptions)
        {
            _userRepository = userRepository;
            _roleRepository = roleRepository;
            _throttleRepository = throttleRepository;
            _passwordHasher = passwordHasher;
            _tokenService = tokenService;
            _unitOfWork = unitOfWork;
            _lockoutOptions = lockoutOptions;
        }

        /// <summary>Máximo de tentativas ao persistir o contador em caso de conflito de concorrência.</summary>
        private const int MaxConcurrencyAttempts = 3;

        /// <summary>
        /// Hash Argon2id dummy no formato Base64(salt):Base64(hash) do Argon2PasswordHasher (salt 16B, hash 32B).
        /// Usado para equalizar o tempo de resposta quando não há senha real a verificar.
        /// </summary>
        private const string DummyPasswordHash =
            "AAECAwQFBgcICQoLDA0ODw==:AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8=";

        public async Task<DTOs.Auth.LoginResponseDto> ExecuteAsync(
            DTOs.Auth.LoginDto dto, string ipAddress, CancellationToken ct = default)
        {
            const string InvalidCredentials = "Credenciais inválidas.";

            var email = dto.Email.Trim().ToLowerInvariant();

            // Concorrência otimista (rowversion): em conflito recarrega o usuário e reaplica a operação
            for (var attempt = 1; attempt <= MaxConcurrencyAttempts; attempt++)
            {
                var user = await _userRepository.GetByEmailAsync(email, ct);

                // E-mail inexistente: Verify contra hash dummy para equalizar o tempo (anti-enumeração)
                if (user is null)
                {
                    _passwordHasher.Verify(dto.Password, DummyPasswordHash);
                    throw new DomainException(InvalidCredentials);
                }

                // Inativo/deletado: Verify dummy equaliza o tempo; nunca usa o hash real nem grava throttle
                if (!user.IsActive || user.IsDeleted)
                {
                    _passwordHasher.Verify(dto.Password, DummyPasswordHash);
                    throw new DomainException(InvalidCredentials);
                }

                var now = DateTime.UtcNow;
                var window = TimeSpan.FromMinutes(_lockoutOptions.LockoutMinutes);

                // Teto global da conta (qualquer IP): nunca verifica a senha real; Verify dummy equaliza o tempo
                if (user.IsLockedOut(now))
                {
                    _passwordHasher.Verify(dto.Password, DummyPasswordHash);
                    throw new DomainException(InvalidCredentials);
                }

                // Bloqueio do par (conta, IP): mesma resposta genérica, sem Verify da senha real
                var throttle = await _throttleRepository.GetAsync(user.Id, ipAddress, ct);
                if (throttle is not null && throttle.IsLockedOut(now))
                {
                    _passwordHasher.Verify(dto.Password, DummyPasswordHash);
                    throw new DomainException(InvalidCredentials);
                }

                try
                {
                    if (!_passwordHasher.Verify(dto.Password, user.PasswordHash))
                    {
                        // Contador global do usuário usa o teto mais alto (cobre ataque distribuído)
                        user.RegisterFailedLogin(_lockoutOptions.GlobalMaxFailedAttempts, window, now);

                        if (throttle is not null)
                        {
                            throttle.RegisterFailure(_lockoutOptions.MaxFailedAttempts, window, now);
                            await _throttleRepository.UpdateAsync(throttle, ct);
                        }
                        else
                        {
                            var created = LoginThrottle.Create(user.Id, ipAddress, now);
                            created.RegisterFailure(_lockoutOptions.MaxFailedAttempts, window, now);
                            // false = tabela cheia de bloqueios ativos: segue só com o teto global (fail-open)
                            await _throttleRepository.TryAddAsync(
                                created, now, window,
                                _lockoutOptions.ThrottleMaxRows,
                                _lockoutOptions.ThrottleCleanupBatchSize, ct);
                        }

                        await _userRepository.UpdateAsync(user, ct);
                        await _unitOfWork.CommitAsync(ct);
                        throw new DomainException(InvalidCredentials);
                    }

                    // Sucesso: zera contadores somente se houver algo a limpar
                    var hasChanges = false;
                    if (user.FailedLoginCount > 0 || user.LockedUntil is not null)
                    {
                        user.ResetFailedLogins();
                        await _userRepository.UpdateAsync(user, ct);
                        hasChanges = true;
                    }

                    if (throttle is not null)
                    {
                        await _throttleRepository.RemoveAsync(throttle, ct);
                        hasChanges = true;
                    }

                    if (hasChanges)
                        await _unitOfWork.CommitAsync(ct);
                }
                catch (ConcurrencyConflictException)
                {
                    if (attempt == MaxConcurrencyAttempts)
                        throw new DomainException(InvalidCredentials);
                    continue;
                }

                // Busca roles para incluir como claims no JWT
                var roles = await _roleRepository.GetRoleNamesByUserIdAsync(user.Id, ct);
                var token = _tokenService.Generate(user, roles);

                return new DTOs.Auth.LoginResponseDto(token, user.Name, user.Email);
            }

            // Inalcançável: o laço sempre retorna ou lança
            throw new DomainException(InvalidCredentials);
        }
    }
}
