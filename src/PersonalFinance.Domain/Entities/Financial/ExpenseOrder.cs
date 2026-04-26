using PersonalFinance.Domain.Entities.Shared;
using PersonalFinance.Domain.Exceptions;

namespace PersonalFinance.Domain.Entities.Financial;

public sealed class ExpenseOrder : EntityBase
{
    public Guid ExpenseId { get; private set; }
    public int Order { get; private set; }

    private ExpenseOrder() { }

    public static ExpenseOrder Create(Guid expenseId, int order)
    {
        if (expenseId == Guid.Empty)
            throw new DomainException("O ExpenseId é obrigatório.");

        if (order < 0)
            throw new DomainException("A ordem deve ser maior ou igual a zero.");

        return new ExpenseOrder { ExpenseId = expenseId, Order = order };
    }

    public void UpdateOrder(int order)
    {
        if (order < 0)
            throw new DomainException("A ordem deve ser maior ou igual a zero.");

        Order = order;
        SetUpdatedAt();
    }
}
