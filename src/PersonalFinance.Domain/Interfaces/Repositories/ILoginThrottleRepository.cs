using PersonalFinance.Domain.Entities.Auth;

namespace PersonalFinance.Domain.Interfaces.Repositories;

/// <summary>
/// Repositório do controle de login por par (conta, IP).
/// Escritas apenas marcam o contexto — a persistência ocorre no IUnitOfWork.
/// Exclusão física (tabela efêmera, sem soft-delete).
/// </summary>
public interface ILoginThrottleRepository
{
    Task<LoginThrottle?> GetAsync(Guid userId, string ipAddress, CancellationToken ct = default);

    /// <summary>
    /// Limpa linhas expiradas (lote), aplica o teto duro removendo as mais antigas não bloqueadas e insere.
    /// Retorna false (fail-open) sem inserir quando o teto não pode ser respeitado sem apagar bloqueios ativos.
    /// </summary>
    Task<bool> TryAddAsync(LoginThrottle throttle, DateTime now, TimeSpan window, int maxRows, int cleanupBatchSize, CancellationToken ct = default);

    Task UpdateAsync(LoginThrottle throttle, CancellationToken ct = default);
    Task RemoveAsync(LoginThrottle throttle, CancellationToken ct = default);
    Task<int> CountAsync(CancellationToken ct = default);
}
