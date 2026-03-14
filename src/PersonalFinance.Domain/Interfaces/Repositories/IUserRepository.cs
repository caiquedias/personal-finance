using PersonalFinance.Domain.Entities.Auth;

namespace PersonalFinance.Domain.Interfaces.Repositories;

/// <summary>
/// Repositório de usuários.
/// Implementação em Infrastructure/Persistence/Repositories/Auth.
/// HasQueryFilter global filtra DeletedAt IS NULL automaticamente.
/// </summary>
public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<bool> ExistsByEmailAsync(string email, CancellationToken ct = default);
    Task AddAsync(User user, CancellationToken ct = default);
    Task UpdateAsync(User user, CancellationToken ct = default);
    Task<IEnumerable<User>> GetAllAsync(CancellationToken ct = default);
}
