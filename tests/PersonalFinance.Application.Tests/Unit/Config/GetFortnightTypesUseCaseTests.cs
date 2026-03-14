using FluentAssertions;
using Moq;
using PersonalFinance.Application.UseCases.Config;
using PersonalFinance.Domain.Entities.Lookup;
using PersonalFinance.Domain.Interfaces.Repositories;
using Xunit;

namespace PersonalFinance.Application.Tests.Unit.Config;

public class GetFortnightTypesUseCaseTests
{
    private readonly Mock<IFortnightTypeRepository> _repository = new();
    private readonly GetFortnightTypesUseCase       _sut;

    public GetFortnightTypesUseCaseTests()
    {
        _sut = new GetFortnightTypesUseCase(_repository.Object);
    }

    [Fact(DisplayName = "Deve retornar tipos com flag IsSystemSeed correto")]
    public async Task Execute_ShouldReturnTypesWithSeedFlag()
    {
        _repository.Setup(r => r.GetAllAsync(default)).ReturnsAsync(new[]
        {
            new FortnightType { Id = 1, Name = "First"  },
            new FortnightType { Id = 2, Name = "Second" },
            new FortnightType { Id = 3, Name = "Third"  },
        });

        var result = (await _sut.ExecuteAsync()).ToList();

        result.First(x => x.Id == 1).IsSystemSeed.Should().BeTrue();
        result.First(x => x.Id == 2).IsSystemSeed.Should().BeTrue();
        result.First(x => x.Id == 3).IsSystemSeed.Should().BeFalse();
    }

    [Fact(DisplayName = "Deve retornar lista vazia se não há registros")]
    public async Task Execute_WithNoTypes_ShouldReturnEmpty()
    {
        _repository.Setup(r => r.GetAllAsync(default))
                   .ReturnsAsync(Enumerable.Empty<FortnightType>());

        var result = await _sut.ExecuteAsync();

        result.Should().BeEmpty();
    }
}
