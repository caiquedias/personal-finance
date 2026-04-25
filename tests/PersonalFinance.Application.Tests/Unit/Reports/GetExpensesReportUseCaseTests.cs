using FluentAssertions;
using Moq;
using PersonalFinance.Application.DTOs.Reports;
using PersonalFinance.Application.Interfaces;
using PersonalFinance.Application.UseCases.Reports;
using Xunit;

namespace PersonalFinance.Application.Tests.Unit.Reports;

public class GetExpensesReportUseCaseTests
{
    private readonly Mock<IReportRepository>    _repo = new();
    private readonly GetExpensesReportUseCase   _sut;

    public GetExpensesReportUseCaseTests()
        => _sut = new GetExpensesReportUseCase(_repo.Object);

    [Fact(DisplayName = "Deve retornar relatório anual agrupado por categoria")]
    public async Task Execute_AnnualReport_ShouldReturnDto()
    {
        var userId = Guid.NewGuid();
        var expected = new ExpensesReportDto(2026, null, [
            new ExpenseByCategoryItemDto(Guid.NewGuid(), "Alimentação", "#F00", 500m)
        ]);
        _repo.Setup(r => r.GetExpensesReportAsync(userId, 2026, null, default)).ReturnsAsync(expected);

        var result = await _sut.ExecuteAsync(userId, 2026, null);

        result.Year.Should().Be(2026);
        result.Month.Should().BeNull();
        result.Items.Should().HaveCount(1);
    }

    [Fact(DisplayName = "Deve retornar relatório mensal agrupado por categoria")]
    public async Task Execute_MonthlyReport_ShouldReturnDto()
    {
        var userId = Guid.NewGuid();
        var expected = new ExpensesReportDto(2026, 4, [
            new ExpenseByCategoryItemDto(Guid.NewGuid(), "Moradia", "#0F0", 1200m)
        ]);
        _repo.Setup(r => r.GetExpensesReportAsync(userId, 2026, 4, default)).ReturnsAsync(expected);

        var result = await _sut.ExecuteAsync(userId, 2026, 4);

        result.Month.Should().Be(4);
        result.Items.Should().HaveCount(1);
    }
}
