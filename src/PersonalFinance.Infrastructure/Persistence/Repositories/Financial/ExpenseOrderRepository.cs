using Microsoft.EntityFrameworkCore;
using PersonalFinance.Domain.Entities.Financial;
using PersonalFinance.Domain.Interfaces.Repositories;
using PersonalFinance.Infrastructure.Persistence.Context;

namespace PersonalFinance.Infrastructure.Persistence.Repositories.Financial;

public sealed class ExpenseOrderRepository : IExpenseOrderRepository
{
    private readonly AppDbContext _context;
    public ExpenseOrderRepository(AppDbContext context) => _context = context;

    public async Task<IEnumerable<ExpenseOrder>> GetByExpenseIdsAsync(
        IEnumerable<Guid> expenseIds, CancellationToken ct = default)
        => await _context.ExpenseOrders
               .Where(o => expenseIds.Contains(o.ExpenseId))
               .ToListAsync(ct);

    public async Task<ExpenseOrder?> GetByExpenseIdAsync(
        Guid expenseId, CancellationToken ct = default)
        => await _context.ExpenseOrders
               .FirstOrDefaultAsync(o => o.ExpenseId == expenseId, ct);

    public async Task AddAsync(ExpenseOrder order, CancellationToken ct = default)
        => await _context.ExpenseOrders.AddAsync(order, ct);

    public Task UpdateAsync(ExpenseOrder order, CancellationToken ct = default)
    {
        _context.ExpenseOrders.Update(order);
        return Task.CompletedTask;
    }
}
