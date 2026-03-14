using FluentAssertions;
using Moq;
using PersonalFinance.Application.DTOs.Admin;
using PersonalFinance.Application.UseCases.Config;
using PersonalFinance.Domain.Entities.Lookup;
using PersonalFinance.Domain.Exceptions;
using PersonalFinance.Domain.Interfaces.Repositories;
using Xunit;

namespace PersonalFinance.Application.Tests.Unit.Config;

public class CreateFortnightTypeUseCaseTests
{
    private readonly Mock<IFortnightTypeRepository> _repository = new();
    private readonly Mock<IUnitOfWork>              _uow        = new();
    private readonly CreateFortnightTypeUseCase     _sut;

    public CreateFortnightTypeUseCaseTests()
    {
        _sut = new CreateFortnightTypeUseCase(_repository.Object, _uow.Object);
    }

    [Fact(DisplayName = "Deve criar tipo de quinzena com dados válidos")]
    public async Task Execute_WithValidData_ShouldCreate()
    {
        _repository.Setup(r => r.ExistsByNameAsync("Third", default)).ReturnsAsync(false);
        _repository.Setup(r => r.GetNextIdAsync(default)).ReturnsAsync(3);

        var result = await _sut.ExecuteAsync(new CreateFortnightTypeDto("Third"));

        result.Id.Should().Be(3);
        result.Name.Should().Be("Third");
        result.IsSystemSeed.Should().BeFalse();
        _repository.Verify(r => r.AddAsync(It.Is<FortnightType>(
            f => f.Id == 3 && f.Name == "Third"), default), Times.Once);
        _uow.Verify(u => u.CommitAsync(default), Times.Once);
    }

    [Fact(DisplayName = "Deve lançar exceção para nome duplicado")]
    public async Task Execute_WithDuplicateName_ShouldThrow()
    {
        _repository.Setup(r => r.ExistsByNameAsync("Third", default)).ReturnsAsync(true);

        var act = () => _sut.ExecuteAsync(new CreateFortnightTypeDto("Third"));

        await act.Should().ThrowAsync<DomainException>();
        _uow.Verify(u => u.CommitAsync(default), Times.Never);
    }

    [Theory(DisplayName = "Deve lançar exceção para nome inválido")]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null)]
    public async Task Execute_WithInvalidName_ShouldThrow(string? name)
    {
        var act = () => _sut.ExecuteAsync(new CreateFortnightTypeDto(name!));

        await act.Should().ThrowAsync<DomainException>();
        _uow.Verify(u => u.CommitAsync(default), Times.Never);
    }
}
