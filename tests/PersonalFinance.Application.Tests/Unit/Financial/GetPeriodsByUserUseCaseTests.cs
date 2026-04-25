using FluentAssertions;
using Moq;
using PersonalFinance.Application.UseCases.Financial.Periods;
using PersonalFinance.Domain.Entities.Financial;
using PersonalFinance.Domain.Interfaces.Repositories;
using Xunit;

namespace PersonalFinance.Application.Tests.Unit.Financial;

public class GetPeriodsByUserUseCaseTests
{
    private readonly Mock<IPeriodRepository>  _repo = new();
    private readonly GetPeriodsByUserUseCase  _sut;

    public GetPeriodsByUserUseCaseTests()
        => _sut = new GetPeriodsByUserUseCase(_repo.Object);

    [Fact(DisplayName = "Deve retornar todos os períodos do usuário")]
    public async Task Execute_WithPeriods_ShouldReturnAll()
    {
        var userId = Guid.NewGuid();
        var periods = new List<Period>
        {
            Period.Create(userId, 2026, 4),
            Period.Create(userId, 2026, 3)
        };
        _repo.Setup(r => r.GetByUserAsync(userId, default)).ReturnsAsync(periods);

        var result = await _sut.ExecuteAsync(userId);

        result.Should().HaveCount(2);
    }

    [Fact(DisplayName = "Deve retornar lista vazia se não houver períodos")]
    public async Task Execute_WithNoPeriods_ShouldReturnEmpty()
    {
        var userId = Guid.NewGuid();
        _repo.Setup(r => r.GetByUserAsync(userId, default)).ReturnsAsync([]);

        var result = await _sut.ExecuteAsync(userId);

        result.Should().BeEmpty();
    }
}
