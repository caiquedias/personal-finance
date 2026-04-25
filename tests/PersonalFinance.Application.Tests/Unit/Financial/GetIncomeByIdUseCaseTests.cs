using FluentAssertions;
using Moq;
using PersonalFinance.Application.UseCases.Financial.Incomes;
using PersonalFinance.Domain.Entities.Financial;
using PersonalFinance.Domain.Enums;
using PersonalFinance.Domain.Interfaces.Repositories;
using Xunit;

namespace PersonalFinance.Application.Tests.Unit.Financial;

public class GetIncomeByIdUseCaseTests
{
    private readonly Mock<IIncomeRepository> _repo = new();
    private readonly GetIncomeByIdUseCase    _sut;

    public GetIncomeByIdUseCaseTests()
        => _sut = new GetIncomeByIdUseCase(_repo.Object);

    [Fact(DisplayName = "Deve retornar receita pelo Id")]
    public async Task Execute_WithExistingIncome_ShouldReturn()
    {
        var incomeId = Guid.NewGuid();
        var userId   = Guid.NewGuid();
        var income   = Income.Create(
            Guid.NewGuid(), userId, FortnightType.First,
            "Salário", 5000m, DateOnly.FromDateTime(DateTime.UtcNow), null);

        _repo.Setup(r => r.GetByIdAndUserAsync(incomeId, userId, default)).ReturnsAsync(income);

        var result = await _sut.ExecuteAsync(incomeId, userId);

        result.Description.Should().Be("Salário");
        result.Amount.Should().Be(5000m);
    }

    [Fact(DisplayName = "Deve lançar exceção se receita não encontrada")]
    public async Task Execute_WithNotFoundIncome_ShouldThrow()
    {
        var incomeId = Guid.NewGuid();
        var userId   = Guid.NewGuid();
        _repo.Setup(r => r.GetByIdAndUserAsync(incomeId, userId, default)).ReturnsAsync((Income?)null);

        var act = () => _sut.ExecuteAsync(incomeId, userId);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }
}
