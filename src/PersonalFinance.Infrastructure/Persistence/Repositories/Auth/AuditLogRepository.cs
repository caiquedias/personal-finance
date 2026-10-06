using Microsoft.EntityFrameworkCore;
using PersonalFinance.Domain.Entities.Auth;
using PersonalFinance.Domain.Interfaces.Repositories;
using PersonalFinance.Infrastructure.Persistence.Context;

namespace PersonalFinance.Infrastructure.Persistence.Repositories.Auth;

/// <summary>
/// Implementação de IAuditLogRepository. Remoção em lote via consulta + RemoveRange
/// (ExecuteDelete não é suportado pelo provider InMemory dos testes).
/// </summary>
public sealed class AuditLogRepository : IAuditLogRepository
{
    private readonly AppDbContext _context;

    public AuditLogRepository(AppDbContext context) => _context = context;

    public async Task AddAsync(AuditLog log, CancellationToken ct = default)
        => await _context.AuditLogs.AddAsync(log, ct);

    public async Task<int> RemoveOlderThanAsync(DateTime cutoff, int batchSize, CancellationToken ct = default)
    {
        var expired = await _context.AuditLogs
            .Where(a => a.CreatedAt < cutoff)
            .OrderBy(a => a.CreatedAt)
            .Take(batchSize)
            .ToListAsync(ct);

        _context.AuditLogs.RemoveRange(expired);
        return expired.Count;
    }
}
