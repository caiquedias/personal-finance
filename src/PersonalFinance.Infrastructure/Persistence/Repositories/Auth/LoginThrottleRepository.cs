using Microsoft.EntityFrameworkCore;
using PersonalFinance.Domain.Entities.Auth;
using PersonalFinance.Domain.Interfaces.Repositories;
using PersonalFinance.Infrastructure.Persistence.Context;

namespace PersonalFinance.Infrastructure.Persistence.Repositories.Auth;

/// <summary>
/// Implementação de ILoginThrottleRepository. Exclusão física; remoções em lote via consulta + RemoveRange
/// (ExecuteDelete não é suportado pelo provider InMemory dos testes).
/// </summary>
public sealed class LoginThrottleRepository : ILoginThrottleRepository
{
    private readonly AppDbContext _context;

    public LoginThrottleRepository(AppDbContext context) => _context = context;

    public async Task<LoginThrottle?> GetAsync(Guid userId, string ipAddress, CancellationToken ct = default)
        => await _context.LoginThrottles
               .FirstOrDefaultAsync(t => t.UserId == userId && t.IpAddress == ipAddress, ct);

    public async Task<bool> TryAddAsync(
        LoginThrottle throttle, DateTime now, TimeSpan window, int maxRows, int cleanupBatchSize,
        CancellationToken ct = default)
    {
        // (1) Limpeza por tempo: expiradas (janela vencida e sem bloqueio ativo), em lote
        var cutoff = now - window;
        var expired = await _context.LoginThrottles
            .Where(t => t.WindowStart < cutoff && (t.LockedUntil == null || t.LockedUntil < now))
            .OrderBy(t => t.WindowStart)
            .Take(cleanupBatchSize)
            .ToListAsync(ct);
        _context.LoginThrottles.RemoveRange(expired);
        var removedIds = expired.Select(t => t.Id).ToHashSet();

        // (2) Teto duro: contagem real descontando o que já foi marcado para remoção
        var count = await _context.LoginThrottles.CountAsync(ct) - removedIds.Count;
        if (count >= maxRows)
        {
            var needed = count - maxRows + 1;
            var candidates = await _context.LoginThrottles
                .Where(t => t.LockedUntil == null || t.LockedUntil < now)
                .OrderBy(t => t.WindowStart)
                .Take(needed + removedIds.Count)
                .ToListAsync(ct);
            var evictable = candidates.Where(t => !removedIds.Contains(t.Id)).Take(needed).ToList();

            // (3) Tudo bloqueado: fail-open — não insere (o teto global do usuário continua valendo)
            if (evictable.Count < needed)
                return false;

            _context.LoginThrottles.RemoveRange(evictable);
        }

        // (4) Insere o novo par
        await _context.LoginThrottles.AddAsync(throttle, ct);
        return true;
    }

    public Task UpdateAsync(LoginThrottle throttle, CancellationToken ct = default)
    {
        _context.LoginThrottles.Update(throttle);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(LoginThrottle throttle, CancellationToken ct = default)
    {
        _context.LoginThrottles.Remove(throttle);
        return Task.CompletedTask;
    }

    public async Task<int> CountAsync(CancellationToken ct = default)
        => await _context.LoginThrottles.CountAsync(ct);
}
