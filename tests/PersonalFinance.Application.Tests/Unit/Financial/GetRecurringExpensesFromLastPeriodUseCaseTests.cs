using FluentAssertions;
using Moq;
using PersonalFinance.Application.UseCases.Financial.Expenses;
using PersonalFinance.Domain.Entities.Financial;
using PersonalFinance.Domain.Enums;
using PersonalFinance.Domain.Interfaces.Repositories;
using Xunit;

namespace PersonalFinance.Application.Tests.Unit.Financial;

public class GetRecurringExpensesFromLastPeriodUseCaseTests
{
    private readonly Mock<IExpenseRepository> _repo = new();

    private static Expense MakeRecurringExpense(Guid userId) =>
        Expense.Create(
            Guid.NewGuid(), userId, Guid.NewGuid(),
            SourceType.Personal, FortnightType.First, PaymentStatus.Pending,
            "Aluguel", 1500m, new DateOnly(2025, 4, 10), null, "obs",
            isRecurring: true);

    [Fact(DisplayName = "Deve retornar apenas despesas recorrentes do último período")]
    public async Task ShouldReturnRecurringExpenses()
    {
        var userId          = Guid.NewGuid();
        var excludePeriodId = Guid.NewGuid();
        var expenses        = new[] { MakeRecurringExpense(userId), MakeRecurringExpense(userId) };

        _repo.Setup(r => r.GetRecurringExpensesFromLastPeriodAsync(userId, excludePeriodId, default))
             .ReturnsAsync(expenses);

        var sut    = new GetRecurringExpensesFromLastPeriodUseCase(_repo.Object);
        var result = (await sut.ExecuteAsync(userId, excludePeriodId)).ToList();

        result.Should().HaveCount(2);
        result.Should().AllSatisfy(r => r.Amount.Should().BeGreaterThan(0));
        result.Should().AllSatisfy(r => r.Description.Should().NotBeNullOrEmpty());
    }

    [Fact(DisplayName = "Deve retornar lista vazia quando não há despesas recorrentes")]
    public async Task ShouldReturnEmptyWhenNoRecurring()
    {
        var userId          = Guid.NewGuid();
        var excludePeriodId = Guid.NewGuid();

        _repo.Setup(r => r.GetRecurringExpensesFromLastPeriodAsync(userId, excludePeriodId, default))
             .ReturnsAsync([]);

        var sut    = new GetRecurringExpensesFromLastPeriodUseCase(_repo.Object);
        var result = await sut.ExecuteAsync(userId, excludePeriodId);

        result.Should().BeEmpty();
    }
}
