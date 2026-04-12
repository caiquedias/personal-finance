using Microsoft.EntityFrameworkCore;
using PersonalFinance.Domain.Entities.Auth;
using PersonalFinance.Domain.Entities.Lookup;
using PersonalFinance.Domain.Interfaces.Repositories;
using PersonalFinance.Infrastructure.Persistence.Context;

namespace PersonalFinance.Infrastructure.Persistence.Repositories.Config;

// IDs de seed protegidos — espelham os enums do Domain
file static class ProtectedIds
{
    public static readonly IReadOnlySet<int> PaymentStatus = new HashSet<int> { 1, 2, 3, 4 };
    public static readonly IReadOnlySet<int> SourceType    = new HashSet<int> { 1, 2 };
    public static readonly IReadOnlySet<int> FortnightType = new HashSet<int> { 1, 2 };
}

// ── PaymentStatus ─────────────────────────────────────────────────────────────

public sealed class PaymentStatusRepository : IPaymentStatusRepository
{
    private readonly AppDbContext _context;
    public PaymentStatusRepository(AppDbContext context) => _context = context;

    public async Task<IEnumerable<PaymentStatus>> GetAllAsync(CancellationToken ct = default)
        => await _context.PaymentStatuses.OrderBy(x => x.Id).ToListAsync(ct);

    public async Task<PaymentStatus?> GetByIdAsync(int id, CancellationToken ct = default)
        => await _context.PaymentStatuses.FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<bool> ExistsByNameAsync(string name, CancellationToken ct = default)
        => await _context.PaymentStatuses
               .AnyAsync(x => x.Name.ToLower() == name.ToLower(), ct);

    public async Task<int> GetNextIdAsync(CancellationToken ct = default)
        => (await _context.PaymentStatuses.MaxAsync(x => (int?)x.Id, ct) ?? 0) + 1;

    public async Task AddAsync(PaymentStatus status, CancellationToken ct = default)
        => await _context.PaymentStatuses.AddAsync(status, ct);

    public Task UpdateAsync(PaymentStatus status, CancellationToken ct = default)
    {
        _context.PaymentStatuses.Update(status);
        return Task.CompletedTask;
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var entity = await _context.PaymentStatuses.FindAsync(new object[] { id }, ct);
        if (entity is not null) _context.PaymentStatuses.Remove(entity);
    }

    public Task<bool> IsSystemSeedAsync(int id, CancellationToken ct = default)
        => Task.FromResult(ProtectedIds.PaymentStatus.Contains(id));
}

// ── SourceType ────────────────────────────────────────────────────────────────

public sealed class SourceTypeRepository : ISourceTypeRepository
{
    private readonly AppDbContext _context;
    public SourceTypeRepository(AppDbContext context) => _context = context;

    public async Task<IEnumerable<SourceType>> GetAllAsync(CancellationToken ct = default)
        => await _context.SourceTypes.OrderBy(x => x.Id).ToListAsync(ct);

    public async Task<SourceType?> GetByIdAsync(int id, CancellationToken ct = default)
        => await _context.SourceTypes.FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<bool> ExistsByNameAsync(string name, CancellationToken ct = default)
        => await _context.SourceTypes
               .AnyAsync(x => x.Name.ToLower() == name.ToLower(), ct);

    public async Task<int> GetNextIdAsync(CancellationToken ct = default)
        => (await _context.SourceTypes.MaxAsync(x => (int?)x.Id, ct) ?? 0) + 1;

    public async Task AddAsync(SourceType sourceType, CancellationToken ct = default)
        => await _context.SourceTypes.AddAsync(sourceType, ct);

    public Task UpdateAsync(SourceType sourceType, CancellationToken ct = default)
    {
        _context.SourceTypes.Update(sourceType);
        return Task.CompletedTask;
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var entity = await _context.SourceTypes.FindAsync(new object[] { id }, ct);
        if (entity is not null) _context.SourceTypes.Remove(entity);
    }

    public Task<bool> IsSystemSeedAsync(int id, CancellationToken ct = default)
        => Task.FromResult(ProtectedIds.SourceType.Contains(id));
}

// ── FortnightType ─────────────────────────────────────────────────────────────

public sealed class FortnightTypeRepository : IFortnightTypeRepository
{
    private readonly AppDbContext _context;
    public FortnightTypeRepository(AppDbContext context) => _context = context;

    public async Task<IEnumerable<FortnightType>> GetAllAsync(CancellationToken ct = default)
        => await _context.FortnightTypes.OrderBy(x => x.Id).ToListAsync(ct);

    public async Task<FortnightType?> GetByIdAsync(int id, CancellationToken ct = default)
        => await _context.FortnightTypes.FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<bool> ExistsByNameAsync(string name, CancellationToken ct = default)
        => await _context.FortnightTypes
               .AnyAsync(x => x.Name.ToLower() == name.ToLower(), ct);

    public async Task<int> GetNextIdAsync(CancellationToken ct = default)
        => (await _context.FortnightTypes.MaxAsync(x => (int?)x.Id, ct) ?? 0) + 1;

    public async Task AddAsync(FortnightType fortnightType, CancellationToken ct = default)
        => await _context.FortnightTypes.AddAsync(fortnightType, ct);

    public Task UpdateAsync(FortnightType fortnightType, CancellationToken ct = default)
    {
        _context.FortnightTypes.Update(fortnightType);
        return Task.CompletedTask;
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var entity = await _context.FortnightTypes.FindAsync(new object[] { id }, ct);
        if (entity is not null) _context.FortnightTypes.Remove(entity);
    }

    public Task<bool> IsSystemSeedAsync(int id, CancellationToken ct = default)
        => Task.FromResult(ProtectedIds.FortnightType.Contains(id));
}

// ── UserRole ──────────────────────────────────────────────────────────────────

public sealed class UserRoleRepository : IUserRoleRepository
{
    private readonly AppDbContext _context;
    public UserRoleRepository(AppDbContext context) => _context = context;

    public async Task<IEnumerable<string>> GetRoleNamesByUserIdAsync(
        Guid userId, CancellationToken ct = default)
        => await _context.UserRoles
               .Where(ur => ur.UserId == userId)
               .Join(_context.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => r.Name)
               .ToListAsync(ct);

    public async Task<IEnumerable<int>> GetRoleIdsByUserIdAsync(
        Guid userId, CancellationToken ct = default)
        => await _context.UserRoles
               .Where(ur => ur.UserId == userId)
               .Select(ur => ur.RoleId)
               .ToListAsync(ct);

    public async Task<bool> UserHasRoleAsync(
        Guid userId, int roleId, CancellationToken ct = default)
        => await _context.UserRoles
               .AnyAsync(ur => ur.UserId == userId && ur.RoleId == roleId, ct);

    public async Task AssignAsync(UserRole userRole, CancellationToken ct = default)
        => await _context.UserRoles.AddAsync(userRole, ct);

    public async Task RemoveAsync(Guid userId, int roleId, CancellationToken ct = default)
    {
        var entity = await _context.UserRoles
            .FirstOrDefaultAsync(ur => ur.UserId == userId && ur.RoleId == roleId, ct);

        if (entity is not null)
            _context.UserRoles.Remove(entity);
    }
}

// ── AdminUser ─────────────────────────────────────────────────────────────────

/// <summary>
/// Repositório admin — acessa TODOS os usuários, incluindo soft-deleted.
/// IgnoreQueryFilters() necessário para listar inativos.
/// </summary>
public sealed class AdminUserRepository : IAdminUserRepository
{
    private readonly AppDbContext _context;
    public AdminUserRepository(AppDbContext context) => _context = context;

    public async Task<IEnumerable<Domain.Entities.Auth.User>> GetAllAsync(
        CancellationToken ct = default)
        => await _context.Users
               .IgnoreQueryFilters() // Admin vê todos, inclusive soft-deleted
               .OrderBy(u => u.Name)
               .ToListAsync(ct);

    public async Task<Domain.Entities.Auth.User?> GetByIdAsync(
        Guid id, CancellationToken ct = default)
        => await _context.Users
               .IgnoreQueryFilters()
               .FirstOrDefaultAsync(u => u.Id == id, ct);

    public async Task<(IEnumerable<Domain.Entities.Auth.User> items, int totalCount)> GetPagedAsync(
        int pageNumber, int pageSize,
        string? name, string? email, bool? isActive,
        CancellationToken ct = default)
    {
        var query = _context.Users
            .IgnoreQueryFilters()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(name))
            query = query.Where(u => u.Name.Contains(name));
        if (!string.IsNullOrWhiteSpace(email))
            query = query.Where(u => u.Email.Contains(email));
        if (isActive.HasValue)
            query = query.Where(u => u.IsActive == isActive.Value);

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderBy(u => u.Name)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }
}
