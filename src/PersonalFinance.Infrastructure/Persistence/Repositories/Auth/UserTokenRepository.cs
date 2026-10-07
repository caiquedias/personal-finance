using Microsoft.EntityFrameworkCore;
using PersonalFinance.Domain.Entities.Auth;
using PersonalFinance.Domain.Enums;
using PersonalFinance.Domain.Interfaces.Repositories;
using PersonalFinance.Infrastructure.Persistence.Context;

namespace PersonalFinance.Infrastructure.Persistence.Repositories.Auth;

/// <summary>
/// Implementação de IUserTokenRepository. Escritas apenas marcam o contexto (persistência no IUnitOfWork).
/// Exclusão física — UserToken não usa soft-delete.
/// </summary>
public sealed class UserTokenRepository(AppDbContext context) : IUserTokenRepository
{
    public async Task<UserToken?> GetLatestAsync(Guid userId, UserTokenPurpose purpose, CancellationToken ct = default)
        => await context.UserTokens
               .Where(t => t.UserId == userId && t.Purpose == purpose)
               .OrderByDescending(t => t.CreatedAt)
               .FirstOrDefaultAsync(ct);

    public async Task AddAsync(UserToken token, CancellationToken ct = default)
        => await context.UserTokens.AddAsync(token, ct);

    public Task UpdateAsync(UserToken token, CancellationToken ct = default)
    {
        context.UserTokens.Update(token);
        return Task.CompletedTask;
    }

    public async Task RemoveAllAsync(Guid userId, UserTokenPurpose purpose, CancellationToken ct = default)
    {
        var tokens = await context.UserTokens
            .Where(t => t.UserId == userId && t.Purpose == purpose)
            .ToListAsync(ct);
        context.UserTokens.RemoveRange(tokens);
    }
}
