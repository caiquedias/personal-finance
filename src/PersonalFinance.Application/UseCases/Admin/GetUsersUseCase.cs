using PersonalFinance.Application.DTOs.Admin;
using PersonalFinance.Domain.Interfaces.Repositories;

namespace PersonalFinance.Application.UseCases.Admin
{
    /// <summary>Lista todos os usuários do sistema com suas roles.</summary>
    public sealed class GetUsersUseCase
    {
        private readonly IAdminUserRepository _userRepository;
        private readonly IUserRoleRepository _roleRepository;

        public GetUsersUseCase(IAdminUserRepository userRepository, IUserRoleRepository roleRepository)
        {
            _userRepository = userRepository;
            _roleRepository = roleRepository;
        }

        public async Task<IEnumerable<AdminUserResponseDto>> ExecuteAsync(CancellationToken ct = default)
        {
            var users = await _userRepository.GetAllAsync(ct);

            var result = new List<AdminUserResponseDto>();
            foreach (var user in users)
            {
                var roles = await _roleRepository.GetRoleNamesByUserIdAsync(user.Id, ct);
                result.Add(new AdminUserResponseDto(
                    user.Id, user.Name, user.Email,
                    user.IsActive, user.IsDeleted,
                    user.CreatedAt, roles));
            }

            return result;
        }
    }
}
