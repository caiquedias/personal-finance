using PersonalFinance.Domain.Entities.Financial;

namespace PersonalFinance.Domain.Interfaces.Repositories
{
    // ── Expense ───────────────────────────────────────────────────────────────────

    /// <summary>Repositório de despesas.</summary>
    public interface IExpenseRepository
    {
        Task<Expense?> GetByIdAsync(Guid id, CancellationToken ct = default);
        Task<Expense?> GetByIdAndUserAsync(Guid id, Guid userId, CancellationToken ct = default);
        Task<IEnumerable<Expense>> GetByPeriodAsync(Guid periodId, Guid userId, CancellationToken ct = default);

        /// <summary>Verifica se existem despesas ativas vinculadas à categoria informada.</summary>
        Task<bool> HasExpensesByCategoryAsync(Guid categoryId, CancellationToken ct = default);

        Task AddAsync(Expense expense, CancellationToken ct = default);
        Task UpdateAsync(Expense expense, CancellationToken ct = default);
    }
}
