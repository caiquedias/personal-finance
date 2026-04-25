using FluentAssertions;
using Moq;
using PersonalFinance.Application.DTOs.Financial;
using PersonalFinance.Application.UseCases.Financial.Incomes;
using PersonalFinance.Domain.Entities.Financial;
using PersonalFinance.Domain.Enums;
using PersonalFinance.Domain.Exceptions;
using PersonalFinance.Domain.Interfaces.Repositories;
using Xunit;

namespace PersonalFinance.Application.Tests.Unit.Financial;

public class GetIncomesByPeriodUseCaseTests
{
    private readonly Mock<IIncomeRepository>  _incomeRepo = new();
    private readonly Mock<IPeriodRepository>  _periodRepo = new();
    private readonly GetIncomesByPeriodUseCase _sut;

    public GetIncomesByPeriodUseCaseTests()
        => _sut = new GetIncomesByPeriodUseCase(_incomeRepo.Object, _periodRepo.Object);

    [Fact(DisplayName = "Deve retornar receitas paginadas do período")]
    public async Task Execute_WithValidPeriod_ShouldReturnPaged()
    {
        var periodId = Guid.NewGuid();
        var userId   = Guid.NewGuid();
        var filter   = new IncomeFilterDto();
        var income   = Income.Create(
            periodId, userId, FortnightType.First,
            "Salário", 5000m, DateOnly.FromDateTime(DateTime.UtcNow), null);

        _periodRepo.Setup(r => r.ExistsByIdAndUserAsync(periodId, userId, default)).ReturnsAsync(true);
        _incomeRepo.Setup(r => r.GetPagedByPeriodAsync(
            periodId, userId, filter.PageNumber, filter.PageSize,
            filter.Description, filter.FortnightType, default))
            .ReturnsAsync((new List<Income> { income }, 1));

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

        var act = () => _sut.ExecuteAsync(periodId, userId, new IncomeFilterDto());

        await act.Should().ThrowAsync<DomainException>().WithMessage("*Período*");
    }
}
