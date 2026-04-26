using PersonalFinance.Application.DTOs.Financial;
using PersonalFinance.Domain.Entities.Financial;
using PersonalFinance.Domain.Exceptions;
using PersonalFinance.Domain.Interfaces.Repositories;

namespace PersonalFinance.Application.UseCases.Financial.Expenses;

/// <summary>
/// Persiste a ordenação de despesas definida pelo usuário via drag and drop.
/// Upsert: cria ExpenseOrder se não existir, atualiza se já existir.
/// Valida que cada ExpenseId pertence ao usuário.
/// </summary>
public sealed class SaveExpenseOrderUseCase
{
    private readonly IExpenseOrderRepository _orderRepository;
    private readonly IExpenseRepository      _expenseRepository;
    private readonly IUnitOfWork             _unitOfWork;

    public SaveExpenseOrderUseCase(
        IExpenseOrderRepository orderRepository,
        IExpenseRepository      expenseRepository,
        IUnitOfWork             unitOfWork)
    {
        _orderRepository   = orderRepository;
        _expenseRepository = expenseRepository;
        _unitOfWork        = unitOfWork;
    }

    public async Task ExecuteAsync(SaveExpenseOrderDto dto, CancellationToken ct = default)
    {
        var items = dto.Items.ToList();

        if (items.Count == 0)
            throw new DomainException("A lista de ordenação não pode estar vazia.");

        // Valida que todos os IDs pertencem ao usuário
        foreach (var item in items)
        {
            var expense = await _expenseRepository.GetByIdAndUserAsync(item.ExpenseId, dto.UserId, ct);
            if (expense is null)
                throw new DomainException(
                    $"Despesa {item.ExpenseId} não encontrada ou sem permissão de acesso.");
        }

        // Upsert de cada item
        foreach (var item in items)
        {
            var existing = await _orderRepository.GetByExpenseIdAsync(item.ExpenseId, ct);
            if (existing is null)
            {
                var order = ExpenseOrder.Create(item.ExpenseId, item.Order);
                await _orderRepository.AddAsync(order, ct);
            }
            else
            {
                existing.UpdateOrder(item.Order);
                await _orderRepository.UpdateAsync(existing, ct);
            }
        }

        await _unitOfWork.CommitAsync(ct);
    }
}
