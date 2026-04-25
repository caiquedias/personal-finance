using FluentAssertions;
using Moq;
using PersonalFinance.Application.UseCases.Financial.Expenses;
using PersonalFinance.Domain.Entities.Financial;
using PersonalFinance.Domain.Enums;
using PersonalFinance.Domain.Interfaces.Repositories;
using Xunit;

namespace PersonalFinance.Application.Tests.Unit.Financial;

public class GetExpenseByIdUseCaseTests
{
    private readonly Mock<IExpenseRepository> _repo = new();
    private readonly GetExpenseByIdUseCase    _sut;

    public GetExpenseByIdUseCaseTests()
        => _sut = new GetExpenseByIdUseCase(_repo.Object);

    [Fact(DisplayName = "Deve retornar despesa pelo Id")]
    public async Task Execute_WithExistingExpense_ShouldReturn()
    {
        var expenseId  = Guid.NewGuid();
        var userId     = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var expense    = Expense.Create(
            Guid.NewGuid(), userId, categoryId,
            SourceType.Personal, FortnightType.First, PaymentStatus.Pending,
            "Mercado", 150m, DateOnly.FromDateTime(DateTime.UtcNow), null, null);

        _repo.Setup(r => r.GetByIdAndUserAsync(expenseId, userId, default)).ReturnsAsync(expense);

        var result = await _sut.ExecuteAsync(expenseId, userId);

        result.Description.Should().Be("Mercado");
        result.Amount.Should().Be(150m);
    }

    [Fact(DisplayName = "Deve lançar exceção se despesa não encontrada")]
    public async Task Execute_WithNotFoundExpense_ShouldThrow()
    {
        var expenseId = Guid.NewGuid();
        var userId    = Guid.NewGuid();
        _repo.Setup(r => r.GetByIdAndUserAsync(expenseId, userId, default)).ReturnsAsync((Expense?)null);

        var act = () => _sut.ExecuteAsync(expenseId, userId);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }
}
