using FluentAssertions;
using Moq;
using PersonalFinance.Application.UseCases.Financial.Periods;
using PersonalFinance.Domain.Entities.Financial;
using PersonalFinance.Domain.Interfaces.Repositories;
using Xunit;

namespace PersonalFinance.Application.Tests.Unit.Financial;

public class GetPeriodByIdUseCaseTests
{
    private readonly Mock<IPeriodRepository> _repo = new();
    private readonly GetPeriodByIdUseCase    _sut;

    public GetPeriodByIdUseCaseTests()
        => _sut = new GetPeriodByIdUseCase(_repo.Object);

    [Fact(DisplayName = "Deve retornar período pelo Id")]
    public async Task Execute_WithExistingPeriod_ShouldReturn()
    {
        var periodId = Guid.NewGuid();
        var userId   = Guid.NewGuid();
        var period   = Period.Create(userId, 2026, 4);

        _repo.Setup(r => r.GetByIdAndUserAsync(periodId, userId, default)).ReturnsAsync(period);

        var result = await _sut.ExecuteAsync(periodId, userId);

        result.Year.Should().Be(2026);
        result.Month.Should().Be(4);
    }

    [Fact(DisplayName = "Deve lançar exceção se período não encontrado")]
    public async Task Execute_WithNotFoundPeriod_ShouldThrow()
    {
        var periodId = Guid.NewGuid();
        var userId   = Guid.NewGuid();
        _repo.Setup(r => r.GetByIdAndUserAsync(periodId, userId, default)).ReturnsAsync((Period?)null);

        var act = () => _sut.ExecuteAsync(periodId, userId);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }
}
