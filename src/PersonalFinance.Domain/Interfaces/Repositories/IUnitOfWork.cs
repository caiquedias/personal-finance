namespace PersonalFinance.Domain.Interfaces.Repositories;

/// <summary>
/// Abstração do Unit of Work.
/// Garante que todas as operações do use case sejam confirmadas em uma única transação.
/// Implementado na Infrastructure via EF Core DbContext.
/// </summary>
public interface IUnitOfWork
{
    Task CommitAsync(CancellationToken ct = default);
}
