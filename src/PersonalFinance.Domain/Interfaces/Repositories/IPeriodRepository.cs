using PersonalFinance.Domain.Entities.Financial;

namespace PersonalFinance.Domain.Interfaces.Repositories;

public interface IPeriodRepository
{
    Task<Period?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Period?> GetByIdAndUserAsync(Guid id, Guid userId, CancellationToken ct = default);
    Task<IEnumerable<Period>> GetByUserAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Retorna o período do usuário para o mês/ano informado, ou null se não existir.
    /// Usado pelo upsert de importação para decidir se cria ou reutiliza o período.
    /// </summary>
    Task<Period?> GetByUserYearMonthAsync(
        Guid userId, int year, int month,
        CancellationToken ct = default);

    Task<bool> ExistsAsync(
        Guid userId, int year, int month,
        CancellationToken ct = default);

    Task AddAsync(Period period, CancellationToken ct = default);

    /// <summary>
    /// Retorna true se o período existir e pertencer ao usuário informado.
    /// </summary>
    Task<bool> ExistsByIdAndUserAsync(Guid id, Guid userId, CancellationToken ct = default);
}
