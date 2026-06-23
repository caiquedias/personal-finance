using PersonalFinance.Domain.Entities.Financial;

namespace PersonalFinance.Domain.Interfaces.Repositories;

public interface IPurgeRepository
{
    Task AddAsync(PurgeRecord record, CancellationToken ct = default);
    Task<IEnumerable<PurgeRecord>> GetByUserAsync(Guid userId, CancellationToken ct = default);
    Task<PurgeRecord?> GetByIdAndUserAsync(Guid id, Guid userId, CancellationToken ct = default);
    Task DeleteAsync(PurgeRecord record, CancellationToken ct = default);
}
