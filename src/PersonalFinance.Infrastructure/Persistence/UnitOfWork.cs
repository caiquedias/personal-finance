using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Domain.Exceptions;
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
    {
        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // Traduz para exceção de domínio — Application não conhece o EF Core
            throw new ConcurrencyConflictException(ex);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Insert concorrente do mesmo par (LoginThrottle) — tratado como conflito para entrar no retry
            throw new ConcurrencyConflictException(ex);
        }
    }

    /// <summary>SQL Server: 2601 (índice único) e 2627 (constraint única).</summary>
    private static bool IsUniqueViolation(DbUpdateException ex)
        => ex.InnerException is SqlException { Number: 2601 or 2627 };
}
