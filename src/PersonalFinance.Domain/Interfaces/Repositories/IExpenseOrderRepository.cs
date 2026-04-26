using PersonalFinance.Domain.Entities.Financial;

namespace PersonalFinance.Domain.Interfaces.Repositories;

public interface IExpenseOrderRepository
{
    Task<IEnumerable<ExpenseOrder>> GetByExpenseIdsAsync(IEnumerable<Guid> expenseIds, CancellationToken ct = default);
    Task<ExpenseOrder?> GetByExpenseIdAsync(Guid expenseId, CancellationToken ct = default);
    Task AddAsync(ExpenseOrder order, CancellationToken ct = default);
    Task UpdateAsync(ExpenseOrder order, CancellationToken ct = default);
}
