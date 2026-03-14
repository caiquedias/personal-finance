using FluentAssertions;
using Moq;
using PersonalFinance.Application.UseCases.Config;
using PersonalFinance.Domain.Entities.Lookup;
using PersonalFinance.Domain.Interfaces.Repositories;
using Xunit;

namespace PersonalFinance.Application.Tests.Unit.Config;

public class GetSourceTypesUseCaseTests
{
    private readonly Mock<ISourceTypeRepository> _repository = new();
    private readonly GetSourceTypesUseCase       _sut;

    public GetSourceTypesUseCaseTests()
    {
        _sut = new GetSourceTypesUseCase(_repository.Object);
    }

    [Fact(DisplayName = "Deve retornar tipos com flag IsSystemSeed correto")]
    public async Task Execute_ShouldReturnTypesWithSeedFlag()
    {
        _repository.Setup(r => r.GetAllAsync(default)).ReturnsAsync(new[]
        {
            new SourceType { Id = 1, Name = "Parental"  },
            new SourceType { Id = 2, Name = "Personal"  },
            new SourceType { Id = 3, Name = "Empresarial" },
        });

        var result = (await _sut.ExecuteAsync()).ToList();

        result.First(x => x.Id == 1).IsSystemSeed.Should().BeTrue();
        result.First(x => x.Id == 2).IsSystemSeed.Should().BeTrue();
        result.First(x => x.Id == 3).IsSystemSeed.Should().BeFalse();
    }

    [Fact(DisplayName = "Os dois seeds fixos devem ser marcados como IsSystemSeed=true")]
    public async Task Execute_FixedSeeds_ShouldBeMarkedAsSystemSeed()
    {
        _repository.Setup(r => r.GetAllAsync(default)).ReturnsAsync(new[]
        {
            new SourceType { Id = 1, Name = "Parental" },
            new SourceType { Id = 2, Name = "Personal" },
        });

        var result = await _sut.ExecuteAsync();

        result.Should().AllSatisfy(x => x.IsSystemSeed.Should().BeTrue());
    }

    [Fact(DisplayName = "Deve retornar lista vazia se não há registros")]
    public async Task Execute_WithNoTypes_ShouldReturnEmpty()
    {
        _repository.Setup(r => r.GetAllAsync(default))
                   .ReturnsAsync(Enumerable.Empty<SourceType>());

        var result = await _sut.ExecuteAsync();

        result.Should().BeEmpty();
    }
}
