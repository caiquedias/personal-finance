using Microsoft.EntityFrameworkCore;
using PersonalFinance.Domain.Entities.Financial;
using PersonalFinance.Domain.Interfaces.Repositories;
using PersonalFinance.Infrastructure.Persistence.Context;

namespace PersonalFinance.Api.Tests.Integration.Fakes;

/// <summary>
/// Substitui o PurgeRepository em testes de integração.
/// Persiste os registros via AppDbContext (InMemory) para que dados sobrevivam
/// entre requisições dentro da mesma instância de factory.
/// </summary>
internal sealed class FakePurgeRepository : IPurgeRepository
{
    private readonly AppDbContext _context;

    public FakePurgeRepository(AppDbContext context) => _context = context;

    public async Task AddAsync(PurgeRecord record, CancellationToken ct = default)
        => await _context.PurgeRecords.AddAsync(record, ct);

    public async Task<IEnumerable<PurgeRecord>> GetByUserAsync(
        Guid userId, CancellationToken ct = default)
        => await _context.PurgeRecords
               .Where(r => r.UserId == userId)
               .ToListAsync(ct);

    public async Task<PurgeRecord?> GetByIdAndUserAsync(
        Guid id, Guid userId, CancellationToken ct = default)
        => await _context.PurgeRecords
               .FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId, ct);

    public Task DeleteAsync(PurgeRecord record, CancellationToken ct = default)
    {
        _context.PurgeRecords.Remove(record);
        return Task.CompletedTask;
    }
}
