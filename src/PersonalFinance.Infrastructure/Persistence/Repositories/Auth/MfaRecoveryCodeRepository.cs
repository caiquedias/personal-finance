using Microsoft.EntityFrameworkCore;
using PersonalFinance.Domain.Entities.Auth;
using PersonalFinance.Domain.Interfaces.Repositories;
using PersonalFinance.Infrastructure.Persistence.Context;

namespace PersonalFinance.Infrastructure.Persistence.Repositories.Auth;

/// <summary>
/// Implementação de IMfaRecoveryCodeRepository. Escritas apenas marcam o contexto —
/// a persistência ocorre no IUnitOfWork. HasQueryFilter global já exclui os removidos (soft-delete).
/// </summary>
public sealed class MfaRecoveryCodeRepository(AppDbContext context) : IMfaRecoveryCodeRepository
{
    public async Task<IReadOnlyList<MfaRecoveryCode>> GetActiveByUserIdAsync(Guid userId, CancellationToken ct = default)
        => await context.MfaRecoveryCodes
               .Where(c => c.UserId == userId && c.UsedAt == null)
               .ToListAsync(ct);

    public async Task AddRangeAsync(IEnumerable<MfaRecoveryCode> codes, CancellationToken ct = default)
        => await context.MfaRecoveryCodes.AddRangeAsync(codes, ct);

    public async Task RemoveAllByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        var codes = await context.MfaRecoveryCodes.Where(c => c.UserId == userId).ToListAsync(ct);
        foreach (var code in codes)
            code.SoftDelete();
    }
}
