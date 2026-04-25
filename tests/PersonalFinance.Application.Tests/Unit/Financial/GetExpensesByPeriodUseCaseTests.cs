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

public class GetExpensesByPeriodUseCaseTests
{
    private readonly Mock<IExpenseRepository> _expenseRepo = new();
    private readonly Mock<IPeriodRepository>  _periodRepo  = new();
    private readonly GetExpensesByPeriodUseCase _sut;

    public GetExpensesByPeriodUseCaseTests()
        => _sut = new GetExpensesByPeriodUseCase(_expenseRepo.Object, _periodRepo.Object);

    [Fact(DisplayName = "Deve retornar despesas paginadas do período")]
    public async Task Execute_WithValidPeriod_ShouldReturnPaged()
    {
        var periodId   = Guid.NewGuid();
        var userId     = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var filter     = new ExpenseFilterDto();
        var expense    = Expense.Create(
            periodId, userId, categoryId,
            SourceType.Personal, FortnightType.First, PaymentStatus.Pending,
            "Aluguel", 1000m, DateOnly.FromDateTime(DateTime.UtcNow), null, null);

        _periodRepo.Setup(r => r.ExistsByIdAndUserAsync(periodId, userId, default)).ReturnsAsync(true);
        _expenseRepo.Setup(r => r.GetPagedByPeriodAsync(
            periodId, userId, filter.PageNumber, filter.PageSize,
            filter.Description, filter.CategoryId,
            filter.PaymentStatus, filter.FortnightType, filter.SourceType, default))
            .ReturnsAsync((new List<Expense> { expense }, 1));

        var result = await _sut.ExecuteAsync(periodId, userId, filter);

        result.Items.Should().HaveCount(1);
        result.TotalCount.Should().Be(1);
    }

    [Fact(DisplayName = "Deve lançar exceção se período não pertence ao usuário")]
    public async Task Execute_WithUnauthorizedPeriod_ShouldThrow()
    {
        var periodId = Guid.NewGuid();
        var userId   = Guid.NewGuid();
        _periodRepo.Setup(r => r.ExistsByIdAndUserAsync(periodId, userId, default)).ReturnsAsync(false);

        var act = () => _sut.ExecuteAsync(periodId, userId, new ExpenseFilterDto());

        await act.Should().ThrowAsync<DomainException>().WithMessage("*Período*");
    }
}
