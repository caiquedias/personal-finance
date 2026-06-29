using PersonalFinance.Application.DTOs.Financial;
using PersonalFinance.Domain.Interfaces.Repositories;

namespace PersonalFinance.Application.UseCases.Financial.Purge;

/// <summary>
/// Retorna todos os registros de expurgo do usuário, ordenados por PurgedAt decrescente,
/// já mapeados para PurgeRecordDto.
/// </summary>
public sealed class GetPurgeRecordsUseCase
{
    private readonly IPurgeRepository _purgeRepository;

    public GetPurgeRecordsUseCase(IPurgeRepository purgeRepository)
    {
        _purgeRepository = purgeRepository;
    }

    public async Task<IEnumerable<PurgeRecordDto>> ExecuteAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        var records = await _purgeRepository.GetByUserAsync(userId, ct);
        return records
            .OrderByDescending(r => r.PurgedAt)
            .Select(r => new PurgeRecordDto
            {
                Id           = r.Id,
                Year         = r.PeriodYear,
                Month        = r.PeriodMonth,
                ItemCount    = r.ExpenseCount + r.IncomeCount,
                TotalIncome  = r.TotalIncome,
                TotalExpense = r.TotalExpense,
                PurgedAt     = r.PurgedAt,
            });
    }
}
