using PersonalFinance.Domain.Entities.Auth;
using PersonalFinance.Domain.Enums;

namespace PersonalFinance.Domain.Interfaces.Repositories;

/// <summary>Repositório dos códigos de uso único. Escritas só marcam o contexto — persistência no IUnitOfWork.</summary>
public interface IUserTokenRepository
{
    /// <summary>Token mais recente do (usuário, propósito), inclusive já usado.</summary>
    Task<UserToken?> GetLatestAsync(Guid userId, UserTokenPurpose purpose, CancellationToken ct = default);

    Task AddAsync(UserToken token, CancellationToken ct = default);

    Task UpdateAsync(UserToken token, CancellationToken ct = default);

    /// <summary>Remoção física de todos os tokens do (usuário, propósito).</summary>
    Task RemoveAllAsync(Guid userId, UserTokenPurpose purpose, CancellationToken ct = default);
}
