using FluentAssertions;
using Moq;
using PersonalFinance.Application.DTOs.Financial;
using PersonalFinance.Application.UseCases.Financial.Expenses;
using PersonalFinance.Domain.Entities.Financial;
using PersonalFinance.Domain.Enums;
using PersonalFinance.Domain.Exceptions;
using PersonalFinance.Domain.Interfaces.Repositories;
using Xunit;

namespace PersonalFinance.Application.Tests.Unit.Financial;

public class SaveExpenseOrderUseCaseTests
{
    private readonly Mock<IExpenseOrderRepository> _orderRepo   = new();
    private readonly Mock<IExpenseRepository>      _expenseRepo = new();
    private readonly Mock<IUnitOfWork>             _uow         = new();
    private readonly SaveExpenseOrderUseCase       _sut;

    private static readonly Guid UserId    = Guid.NewGuid();
    private static readonly Guid ExpenseId = Guid.NewGuid();

    public SaveExpenseOrderUseCaseTests() =>
        _sut = new SaveExpenseOrderUseCase(_orderRepo.Object, _expenseRepo.Object, _uow.Object);

    private static Expense BuildExpense() => Expense.Create(
        Guid.NewGuid(), UserId, Guid.NewGuid(),
        SourceType.Personal, FortnightType.First, PaymentStatus.Pending,
        "Aluguel", 1000m, DateOnly.FromDateTime(DateTime.UtcNow), null, null);

    [Fact(DisplayName = "Deve criar nova ExpenseOrder quando não existe")]
    public async Task Execute_WhenNoExistingOrder_ShouldCreate()
    {
        var expense = BuildExpense();
        _expenseRepo.Setup(r => r.GetByIdAndUserAsync(ExpenseId, UserId, default))
                    .ReturnsAsync(expense);
        _orderRepo.Setup(r => r.GetByExpenseIdAsync(ExpenseId, default))
                  .ReturnsAsync((ExpenseOrder?)null);

        var dto = new SaveExpenseOrderDto(UserId, [new ExpenseOrderItemDto(ExpenseId, 0)]);
        await _sut.ExecuteAsync(dto);

        _orderRepo.Verify(r => r.AddAsync(It.Is<ExpenseOrder>(o => o.ExpenseId == ExpenseId && o.Order == 0), default), Times.Once);
        _uow.Verify(u => u.CommitAsync(default), Times.Once);
    }

    [Fact(DisplayName = "Deve atualizar ExpenseOrder existente")]
    public async Task Execute_WhenOrderExists_ShouldUpdate()
    {
        var expense = BuildExpense();
        var existingOrder = ExpenseOrder.Create(ExpenseId, 0);

        _expenseRepo.Setup(r => r.GetByIdAndUserAsync(ExpenseId, UserId, default))
                    .ReturnsAsync(expense);
        _orderRepo.Setup(r => r.GetByExpenseIdAsync(ExpenseId, default))
                  .ReturnsAsync(existingOrder);

        var dto = new SaveExpenseOrderDto(UserId, [new ExpenseOrderItemDto(ExpenseId, 3)]);
        await _sut.ExecuteAsync(dto);

        existingOrder.Order.Should().Be(3);
        _orderRepo.Verify(r => r.UpdateAsync(existingOrder, default), Times.Once);
        _uow.Verify(u => u.CommitAsync(default), Times.Once);
    }

    [Fact(DisplayName = "Deve lançar exceção para lista vazia")]
    public async Task Execute_WithEmptyList_ShouldThrow()
    {
        var dto = new SaveExpenseOrderDto(UserId, []);
        var act = () => _sut.ExecuteAsync(dto);
        await act.Should().ThrowAsync<DomainException>().WithMessage("*vazia*");
        _uow.Verify(u => u.CommitAsync(default), Times.Never);
    }

    [Fact(DisplayName = "Deve lançar exceção quando despesa não pertence ao usuário")]
    public async Task Execute_WithUnauthorizedExpense_ShouldThrow()
    {
        _expenseRepo.Setup(r => r.GetByIdAndUserAsync(ExpenseId, UserId, default))
                    .ReturnsAsync((Expense?)null);

        var dto = new SaveExpenseOrderDto(UserId, [new ExpenseOrderItemDto(ExpenseId, 0)]);
        var act = () => _sut.ExecuteAsync(dto);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*permissão*");
        _uow.Verify(u => u.CommitAsync(default), Times.Never);
    }
}
