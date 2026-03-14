using Microsoft.EntityFrameworkCore;
using PersonalFinance.Domain.Entities.Auth;
using PersonalFinance.Domain.Entities.Config;
using PersonalFinance.Domain.Interfaces.Repositories;
using PersonalFinance.Infrastructure.Persistence.Context;

namespace PersonalFinance.Infrastructure.Persistence.Repositories.Auth;

/// <summary>
/// Implementação concreta de IUserRepository.
/// HasQueryFilter global já filtra DeletedAt IS NULL — sem Where() manual.
/// </summary>
public sealed class UserRepository : IUserRepository
{
    private readonly AppDbContext _context;

    public UserRepository(AppDbContext context) => _context = context;

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

    public async Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
        => await _context.Users
               .FirstOrDefaultAsync(u => u.Email == email.ToLowerInvariant(), ct);

    public async Task<IEnumerable<User>> GetAllAsync(CancellationToken ct = default)
        => _context.Users.AsEnumerable();

    public async Task<bool> ExistsByEmailAsync(string email, CancellationToken ct = default)
        => await _context.Users
               .AnyAsync(u => u.Email == email.ToLowerInvariant(), ct);

    public async Task AddAsync(User user, CancellationToken ct = default)
        => await _context.Users.AddAsync(user, ct);

    public Task UpdateAsync(User user, CancellationToken ct = default)
    {
        _context.Users.Update(user);
        return Task.CompletedTask;
    }
}

