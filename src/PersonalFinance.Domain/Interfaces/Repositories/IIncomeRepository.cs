using PersonalFinance.Domain.Entities.Financial;

namespace PersonalFinance.Domain.Interfaces.Repositories
{
    // ── Income ────────────────────────────────────────────────────────────────────

    /// <summary>Repositório de receitas.</summary>
    public interface IIncomeRepository
    {
        Task<Income?> GetByIdAsync(Guid id, CancellationToken ct = default);
        Task<Income?> GetByIdAndUserAsync(Guid id, Guid userId, CancellationToken ct = default);
        Task<IEnumerable<Income>> GetByPeriodAsync(Guid periodId, Guid userId, CancellationToken ct = default);

        Task AddAsync(Income income, CancellationToken ct = default);
        Task UpdateAsync(Income income, CancellationToken ct = default);
    }
}
