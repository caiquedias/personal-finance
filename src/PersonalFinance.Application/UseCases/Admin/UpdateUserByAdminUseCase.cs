using PersonalFinance.Application.DTOs.Admin;
using PersonalFinance.Domain.Interfaces.Repositories;

namespace PersonalFinance.Application.UseCases.Admin;

/// <summary>Atualiza o nome de um usuário.</summary>
public sealed class UpdateUserByAdminUseCase
{
    private readonly IAdminUserRepository _userRepository;
    private readonly IUserRoleRepository  _roleRepository;
    private readonly IUnitOfWork          _uow;

    public UpdateUserByAdminUseCase(
        IAdminUserRepository userRepository,
        IUserRoleRepository  roleRepository,
        IUnitOfWork          uow)
    {
        _userRepository = userRepository;
        _roleRepository = roleRepository;
        _uow            = uow;
    }

    public async Task<AdminUserResponseDto> ExecuteAsync(
        UpdateUserByAdminDto dto, CancellationToken ct = default)
    {
        var user = await _userRepository.GetByIdAsync(dto.UserId, ct)
            ?? throw new KeyNotFoundException("Usuário não encontrado.");

        user.UpdateName(dto.Name);
        await _userRepository.UpdateAsync(user, ct);
        await _uow.CommitAsync(ct);

        var roles = await _roleRepository.GetRoleNamesByUserIdAsync(user.Id, ct);

        return new AdminUserResponseDto(
            user.Id,
            user.Name,
            user.Email,
            user.IsActive,
            user.DeletedAt.HasValue,
            user.CreatedAt,
            roles);
    }
}
