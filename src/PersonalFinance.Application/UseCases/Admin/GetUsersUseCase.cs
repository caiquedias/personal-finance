using PersonalFinance.Application.DTOs;
using PersonalFinance.Application.DTOs.Admin;
using PersonalFinance.Domain.Interfaces.Repositories;

namespace PersonalFinance.Application.UseCases.Admin
{
    /// <summary>Lista usuários do sistema com paginação e filtros.</summary>
    public sealed class GetUsersUseCase
    {
        private readonly IAdminUserRepository _userRepository;
        private readonly IUserRoleRepository _roleRepository;

        public GetUsersUseCase(IAdminUserRepository userRepository, IUserRoleRepository roleRepository)
        {
            _userRepository = userRepository;
            _roleRepository = roleRepository;
        }

        public async Task<PagedResult<AdminUserResponseDto>> ExecuteAsync(
            AdminUserFilterDto filter, CancellationToken ct = default)
        {
            var (users, totalCount) = await _userRepository.GetPagedAsync(
                filter.PageNumber, filter.PageSize,
                filter.Name, filter.Email, filter.IsActive, ct);

            var result = new List<AdminUserResponseDto>();
            foreach (var user in users)
            {
                var roles = await _roleRepository.GetRoleNamesByUserIdAsync(user.Id, ct);
                result.Add(new AdminUserResponseDto(
                    user.Id, user.Name, user.Email,
                    user.IsActive, user.IsDeleted,
                    user.CreatedAt, roles));
            }

            return new PagedResult<AdminUserResponseDto>(result, totalCount, filter.PageNumber, filter.PageSize);
        }
    }
}
