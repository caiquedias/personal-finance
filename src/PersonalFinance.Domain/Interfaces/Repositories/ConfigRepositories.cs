using PersonalFinance.Domain.Entities.Auth;
using PersonalFinance.Domain.Entities.Lookup;

namespace PersonalFinance.Domain.Interfaces.Repositories;

// ══════════════════════════════════════════════════════════════════════════════
// LOOKUP REPOSITORIES
// Operações de leitura + criação. IDs 1-N de seed são protegidos (não deletáveis).
// ══════════════════════════════════════════════════════════════════════════════

public interface IPaymentStatusRepository
{
    Task<IEnumerable<PaymentStatus>> GetAllAsync(CancellationToken ct = default);
    Task<PaymentStatus?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<bool> ExistsByNameAsync(string name, CancellationToken ct = default);
    Task<int> GetNextIdAsync(CancellationToken ct = default);
    Task AddAsync(PaymentStatus status, CancellationToken ct = default);
    Task UpdateAsync(PaymentStatus status, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
    Task<bool> IsSystemSeedAsync(int id, CancellationToken ct = default);
}

public interface ISourceTypeRepository
{
    Task<IEnumerable<SourceType>> GetAllAsync(CancellationToken ct = default);
    Task<SourceType?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<bool> ExistsByNameAsync(string name, CancellationToken ct = default);
    Task<int> GetNextIdAsync(CancellationToken ct = default);
    Task AddAsync(SourceType sourceType, CancellationToken ct = default);
    Task UpdateAsync(SourceType sourceType, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
    Task<bool> IsSystemSeedAsync(int id, CancellationToken ct = default);
}

public interface IFortnightTypeRepository
{
    Task<IEnumerable<FortnightType>> GetAllAsync(CancellationToken ct = default);
    Task<FortnightType?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<bool> ExistsByNameAsync(string name, CancellationToken ct = default);
    Task<int> GetNextIdAsync(CancellationToken ct = default);
    Task AddAsync(FortnightType fortnightType, CancellationToken ct = default);
    Task UpdateAsync(FortnightType fortnightType, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
    Task<bool> IsSystemSeedAsync(int id, CancellationToken ct = default);
}

// ══════════════════════════════════════════════════════════════════════════════
// USER ROLE REPOSITORY
// ══════════════════════════════════════════════════════════════════════════════

public interface IUserRoleRepository
{
    Task<IEnumerable<string>> GetRoleNamesByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<IEnumerable<int>> GetRoleIdsByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<bool> UserHasRoleAsync(Guid userId, int roleId, CancellationToken ct = default);
    Task AssignAsync(UserRole userRole, CancellationToken ct = default);
    Task RemoveAsync(Guid userId, int roleId, CancellationToken ct = default);
}

// ══════════════════════════════════════════════════════════════════════════════
// ADMIN USER REPOSITORY — operações que o admin faz sobre outros usuários
// ══════════════════════════════════════════════════════════════════════════════

public interface IAdminUserRepository
{
    Task<IEnumerable<User>> GetAllAsync(CancellationToken ct = default);
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default);
}
