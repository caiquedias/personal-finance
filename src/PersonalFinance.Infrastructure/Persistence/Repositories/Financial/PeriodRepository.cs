using Microsoft.EntityFrameworkCore;
using PersonalFinance.Domain.Entities.Financial;
using PersonalFinance.Domain.Interfaces.Repositories;
using PersonalFinance.Infrastructure.Persistence.Context;

namespace PersonalFinance.Infrastructure.Persistence.Repositories.Financial;

public sealed class PeriodRepository : IPeriodRepository
{
    private readonly AppDbContext _context;
    public PeriodRepository(AppDbContext context) => _context = context;

    public async Task<Period?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.Periods.FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<Period?> GetByIdAndUserAsync(
        Guid id, Guid userId, CancellationToken ct = default)
        => await _context.Periods
               .FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId, ct);

    public async Task<IEnumerable<Period>> GetByUserAsync(
        Guid userId, CancellationToken ct = default)
        => await _context.Periods
               .Where(p => p.UserId == userId)
               .OrderByDescending(p => p.Year)
               .ThenByDescending(p => p.Month)
               .ToListAsync(ct);

    /// <summary>
    /// Retorna o período do usuário para o mês/ano — ignora o soft delete filter
    /// para que períodos desativados também sejam encontrados durante a importação.
    /// </summary>
    public async Task<Period?> GetByUserYearMonthAsync(
        Guid userId, int year, int month,
        CancellationToken ct = default)
        => await _context.Periods
               .IgnoreQueryFilters()          // inclui soft-deleted para não duplicar
               .FirstOrDefaultAsync(
                   p => p.UserId == userId &&
                        p.Year   == year    &&
                        p.Month  == month, ct);

    public async Task<bool> ExistsAsync(
        Guid userId, int year, int month,
        CancellationToken ct = default)
        => await _context.Periods
               .AnyAsync(p => p.UserId == userId &&
                              p.Year   == year    &&
                              p.Month  == month, ct);

    public async Task AddAsync(Period period, CancellationToken ct = default)
        => await _context.Periods.AddAsync(period, ct);

    public async Task<bool> ExistsByIdAndUserAsync(
    Guid id, Guid userId, CancellationToken ct = default)
    => await _context.Periods
           .AnyAsync(p => p.Id == id && p.UserId == userId, ct);
}
