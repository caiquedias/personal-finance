using PersonalFinance.Domain.Entities.Auth;

namespace PersonalFinance.Domain.Interfaces.Repositories;

/// <summary>
/// Repositório da trilha de auditoria (insert-only).
/// Escritas apenas marcam o contexto — a persistência ocorre no IUnitOfWork (mesma transação da ação).
/// </summary>
public interface IAuditLogRepository
{
    Task AddAsync(AuditLog log, CancellationToken ct = default);

    /// <summary>
    /// Marca para remoção até <paramref name="batchSize"/> linhas estritamente anteriores a
    /// <paramref name="cutoff"/> (mais antigas primeiro). Retorna a quantidade marcada.
    /// </summary>
    Task<int> RemoveOlderThanAsync(DateTime cutoff, int batchSize, CancellationToken ct = default);
}
