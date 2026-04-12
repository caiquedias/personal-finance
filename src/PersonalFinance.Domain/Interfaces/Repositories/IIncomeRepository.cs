using PersonalFinance.Domain.Entities.Financial;
using PersonalFinance.Domain.Enums;

namespace PersonalFinance.Domain.Interfaces.Repositories;

public interface IIncomeRepository
{
    Task<Income?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Income?> GetByIdAndUserAsync(Guid id, Guid userId, CancellationToken ct = default);
    Task<IEnumerable<Income>> GetByPeriodAsync(Guid periodId, Guid userId, CancellationToken ct = default);
    Task<(IEnumerable<Income> Items, int TotalCount)> GetPagedByPeriodAsync(
        Guid periodId, Guid userId,
        int pageNumber, int pageSize,
        string? description, FortnightType? fortnightType,
        CancellationToken ct = default);

    /// <summary>
    /// Busca uma receita pela chave de idempotência de importação:
    /// PeriodId + Descrição (case-insensitive) + Valor + Quinzena.
    /// Usado para upsert inteligente durante re-importação da planilha legada.
    /// </summary>
    Task<Income?> FindByImportKeyAsync(
        Guid          periodId,
        string        description,
        decimal       amount,
        FortnightType fortnightType,
        CancellationToken ct = default);

    Task AddAsync(Income income, CancellationToken ct = default);

    Task UpdateAsync(Income income, CancellationToken ct = default);
}
