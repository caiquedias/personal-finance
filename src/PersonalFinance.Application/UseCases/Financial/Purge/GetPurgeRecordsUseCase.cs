using PersonalFinance.Domain.Entities.Financial;
using PersonalFinance.Domain.Interfaces.Repositories;

namespace PersonalFinance.Application.UseCases.Financial.Purge;

/// <summary>
/// Retorna todos os registros de expurgo do usuário, ordenados por PurgedAt decrescente.
/// </summary>
public sealed class GetPurgeRecordsUseCase
{
    private readonly IPurgeRepository _purgeRepository;

    public GetPurgeRecordsUseCase(IPurgeRepository purgeRepository)
    {
        _purgeRepository = purgeRepository;
    }

    public async Task<IEnumerable<PurgeRecord>> ExecuteAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        var records = await _purgeRepository.GetByUserAsync(userId, ct);
        return records.OrderByDescending(r => r.PurgedAt);
    }
}
