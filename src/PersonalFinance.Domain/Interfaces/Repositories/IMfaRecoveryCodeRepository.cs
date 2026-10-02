using PersonalFinance.Domain.Entities.Auth;

namespace PersonalFinance.Domain.Interfaces.Repositories;

/// <summary>
/// Repositório dos recovery codes de MFA. Escritas apenas marcam o contexto —
/// a persistência ocorre no IUnitOfWork.
/// </summary>
public interface IMfaRecoveryCodeRepository
{
    /// <summary>Códigos do usuário ainda não usados e não removidos.</summary>
    Task<IReadOnlyList<MfaRecoveryCode>> GetActiveByUserIdAsync(Guid userId, CancellationToken ct = default);

    Task AddRangeAsync(IEnumerable<MfaRecoveryCode> codes, CancellationToken ct = default);

    /// <summary>Soft-delete de todos os códigos do usuário (usados ou não).</summary>
    Task RemoveAllByUserIdAsync(Guid userId, CancellationToken ct = default);
}
