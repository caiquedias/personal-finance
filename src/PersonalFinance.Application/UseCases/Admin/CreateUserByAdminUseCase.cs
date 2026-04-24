using PersonalFinance.Application.DTOs.Admin;
using PersonalFinance.Domain.Entities.Auth;
using PersonalFinance.Domain.Exceptions;
using PersonalFinance.Domain.Interfaces.Repositories;
using PersonalFinance.Domain.Interfaces.Services;

namespace PersonalFinance.Application.UseCases.Admin;

/// <summary>Cria um novo usuário com role padrão User.</summary>
public sealed class CreateUserByAdminUseCase
{
    private readonly IAdminUserRepository _userRepository;
    private readonly IUserRoleRepository  _roleRepository;
    private readonly IPasswordHasher      _hasher;
    private readonly IUnitOfWork          _uow;

    public CreateUserByAdminUseCase(
        IAdminUserRepository userRepository,
        IUserRoleRepository  roleRepository,
        IPasswordHasher      hasher,
        IUnitOfWork          uow)
    {
        _userRepository = userRepository;
        _roleRepository = roleRepository;
        _hasher         = hasher;
        _uow            = uow;
    }

    public async Task<AdminUserResponseDto> ExecuteAsync(
        CreateUserByAdminDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Password) || dto.Password.Length < 8)
            throw new DomainException("A senha deve ter no mínimo 8 caracteres.");

        var exists = await _userRepository.ExistsByEmailAsync(dto.Email, ct);
        if (exists)
            throw new DomainException($"Já existe um usuário com o e-mail '{dto.Email}'.");

        var hash = _hasher.Hash(dto.Password);
        var user = User.Create(dto.Name, dto.Email, hash);

        await _userRepository.AddAsync(user, ct);

        // role padrão: User (Id = 2)
        await _roleRepository.AssignAsync(new UserRole
        {
            UserId     = user.Id,
            RoleId     = 2,
            AssignedAt = DateTime.UtcNow
        }, ct);

        await _uow.CommitAsync(ct);

        return new AdminUserResponseDto(
            user.Id,
            user.Name,
            user.Email,
            user.IsActive,
            user.DeletedAt.HasValue,
            user.CreatedAt,
            ["User"]);
    }
}
