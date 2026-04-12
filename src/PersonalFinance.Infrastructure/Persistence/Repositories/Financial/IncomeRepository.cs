using Microsoft.EntityFrameworkCore;
using PersonalFinance.Domain.Entities.Financial;
using PersonalFinance.Domain.Enums;
using PersonalFinance.Domain.Interfaces.Repositories;
using PersonalFinance.Infrastructure.Persistence.Context;

namespace PersonalFinance.Infrastructure.Persistence.Repositories.Financial;

public sealed class IncomeRepository : IIncomeRepository
{
    private readonly AppDbContext _context;
    public IncomeRepository(AppDbContext context) => _context = context;

    public async Task<Income?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.Incomes.FirstOrDefaultAsync(i => i.Id == id, ct);

    public async Task<Income?> GetByIdAndUserAsync(
        Guid id, Guid userId, CancellationToken ct = default)
        => await _context.Incomes
               .FirstOrDefaultAsync(i => i.Id == id && i.UserId == userId, ct);

    public async Task<IEnumerable<Income>> GetByPeriodAsync(
        Guid periodId, Guid userId, CancellationToken ct = default)
        => await _context.Incomes
               .Where(i => i.PeriodId == periodId && i.UserId == userId)
               .OrderBy(i => i.FortnightType)
               .ThenBy(i => i.ReceivedAt)
               .ToListAsync(ct);

    public async Task<(IEnumerable<Income> Items, int TotalCount)> GetPagedByPeriodAsync(
        Guid periodId, Guid userId,
        int pageNumber, int pageSize,
        string? description, FortnightType? fortnightType,
        CancellationToken ct = default)
    {
        var query = _context.Incomes
            .Where(i => i.PeriodId == periodId && i.UserId == userId);

        if (!string.IsNullOrWhiteSpace(description))
            query = query.Where(i => i.Description.Contains(description));

        if (fortnightType.HasValue)
            query = query.Where(i => i.FortnightType == fortnightType.Value);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderBy(i => i.FortnightType)
            .ThenBy(i => i.ReceivedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    /// <summary>
    /// Busca por chave de idempotência: PeriodId + Descrição + Valor + Quinzena.
    /// Ignora soft delete para detectar registros desativados e evitar duplicatas.
    /// </summary>
    public async Task<Income?> FindByImportKeyAsync(
        Guid periodId,
        string description,
        decimal amount,
        FortnightType fortnightType,
        CancellationToken ct = default)
        => await _context.Incomes
               .IgnoreQueryFilters()
               .FirstOrDefaultAsync(i =>
                   i.PeriodId == periodId &&
                   i.Description.ToLower() == description.ToLower() &&
                   i.Amount == amount &&
                   i.FortnightType == fortnightType, ct);

    public async Task AddAsync(Income income, CancellationToken ct = default)
        => await _context.Incomes.AddAsync(income, ct);

    public Task UpdateAsync(Income income, CancellationToken ct = default)
    {
        _context.Incomes.Update(income);
        return Task.CompletedTask;
    }
}
