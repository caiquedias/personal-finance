using PersonalFinance.Domain.Interfaces.Repositories;
using PersonalFinance.Infrastructure.Persistence.Context;

namespace PersonalFinance.Infrastructure.Persistence;

/// <summary>
/// Implementação de IUnitOfWork via EF Core DbContext.
/// Garante que todas as operações de um use case sejam persistidas
/// em uma única transação ao chamar CommitAsync().
/// </summary>
public sealed class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;

    public UnitOfWork(AppDbContext context) => _context = context;

    public async Task CommitAsync(CancellationToken ct = default)
        => await _context.SaveChangesAsync(ct);
}
