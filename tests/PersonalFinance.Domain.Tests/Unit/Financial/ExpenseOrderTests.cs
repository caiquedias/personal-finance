using FluentAssertions;
using PersonalFinance.Domain.Entities.Financial;
using PersonalFinance.Domain.Exceptions;
using Xunit;

namespace PersonalFinance.Domain.Tests.Unit.Financial;

public class ExpenseOrderTests
{
    private static readonly Guid ExpenseId = Guid.NewGuid();

    [Fact(DisplayName = "Deve criar ExpenseOrder com dados válidos")]
    public void Create_WithValidData_ShouldSucceed()
    {
        var order = ExpenseOrder.Create(ExpenseId, 0);

        order.ExpenseId.Should().Be(ExpenseId);
        order.Order.Should().Be(0);
        order.IsActive.Should().BeTrue();
        order.Id.Should().NotBeEmpty();
    }

    [Fact(DisplayName = "Deve lançar exceção para ExpenseId vazio")]
    public void Create_WithEmptyExpenseId_ShouldThrow()
    {
        var act = () => ExpenseOrder.Create(Guid.Empty, 0);
        act.Should().Throw<DomainException>().WithMessage("*ExpenseId*");
    }

    [Fact(DisplayName = "Deve lançar exceção para Order negativo")]
    public void Create_WithNegativeOrder_ShouldThrow()
    {
        var act = () => ExpenseOrder.Create(ExpenseId, -1);
        act.Should().Throw<DomainException>().WithMessage("*ordem*");
    }

    [Fact(DisplayName = "Deve atualizar Order com valor válido")]
    public void UpdateOrder_WithValidValue_ShouldUpdate()
    {
        var order = ExpenseOrder.Create(ExpenseId, 0);
        order.UpdateOrder(5);
        order.Order.Should().Be(5);
    }

    [Fact(DisplayName = "Deve lançar exceção ao atualizar Order com valor negativo")]
    public void UpdateOrder_WithNegativeValue_ShouldThrow()
    {
        var order = ExpenseOrder.Create(ExpenseId, 0);
        var act = () => order.UpdateOrder(-1);
        act.Should().Throw<DomainException>().WithMessage("*ordem*");
    }
}
