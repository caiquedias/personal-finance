using PersonalFinance.Domain.Entities.Financial;

namespace PersonalFinance.Domain.Interfaces.Repositories
{
    // ── Period ────────────────────────────────────────────────────────────────────

    /// <summary>Repositório de períodos mensais.</summary>
    public interface IPeriodRepository
    {
        Task<Period?> GetByIdAsync(Guid id, CancellationToken ct = default);
        Task<Period?> GetByIdAndUserAsync(Guid id, Guid userId, CancellationToken ct = default);
        Task<Period?> GetByYearMonthAsync(Guid userId, int year, int month, CancellationToken ct = default);
        Task<IEnumerable<Period>> GetByUserAsync(Guid userId, CancellationToken ct = default);

        /// <summary>Verifica se já existe um período para o usuário no mês/ano informado.</summary>
        Task<bool> ExistsAsync(Guid userId, int year, int month, CancellationToken ct = default);

        /// <summary>Verifica se o período existe e pertence ao usuário.</summary>
        Task<bool> ExistsByIdAndUserAsync(Guid periodId, Guid userId, CancellationToken ct = default);

        Task AddAsync(Period period, CancellationToken ct = default);
    }
}
