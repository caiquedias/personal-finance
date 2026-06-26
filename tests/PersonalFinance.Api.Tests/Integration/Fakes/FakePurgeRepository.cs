using PersonalFinance.Domain.Entities.Financial;
using PersonalFinance.Domain.Interfaces.Repositories;

namespace PersonalFinance.Api.Tests.Integration.Fakes;

/// <summary>
/// Substitui o PurgeRepository em testes de integração.
/// Usa List em memória com LINQ puro — sem SqlQueryRaw ou provider relacional.
/// </summary>
internal sealed class FakePurgeRepository : IPurgeRepository
{
    private readonly List<PurgeRecord> _store = new();

    public Task AddAsync(PurgeRecord record, CancellationToken ct = default)
    {
        _store.Add(record);
        return Task.CompletedTask;
    }

    public Task<IEnumerable<PurgeRecord>> GetByUserAsync(Guid userId, CancellationToken ct = default)
    {
        IEnumerable<PurgeRecord> result = _store.Where(r => r.UserId == userId).ToList();
        return Task.FromResult(result);
    }

    public Task<PurgeRecord?> GetByIdAndUserAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var record = _store.FirstOrDefault(r => r.Id == id && r.UserId == userId);
        return Task.FromResult(record);
    }

    public Task DeleteAsync(PurgeRecord record, CancellationToken ct = default)
    {
        _store.Remove(record);
        return Task.CompletedTask;
    }
}
