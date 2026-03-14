using FluentAssertions;
using Moq;
using PersonalFinance.Application.DTOs.Admin;
using PersonalFinance.Application.UseCases.Config;
using PersonalFinance.Domain.Entities.Lookup;
using PersonalFinance.Domain.Exceptions;
using PersonalFinance.Domain.Interfaces.Repositories;
using Xunit;

namespace PersonalFinance.Application.Tests.Unit.Config;

public class CreateSourceTypeUseCaseTests
{
    private readonly Mock<ISourceTypeRepository> _repository = new();
    private readonly Mock<IUnitOfWork>           _uow        = new();
    private readonly CreateSourceTypeUseCase     _sut;

    public CreateSourceTypeUseCaseTests()
    {
        _sut = new CreateSourceTypeUseCase(_repository.Object, _uow.Object);
    }

    [Fact(DisplayName = "Deve criar tipo de fonte com dados válidos")]
    public async Task Execute_WithValidData_ShouldCreate()
    {
        _repository.Setup(r => r.ExistsByNameAsync("Empresarial", default)).ReturnsAsync(false);
        _repository.Setup(r => r.GetNextIdAsync(default)).ReturnsAsync(3);

        var result = await _sut.ExecuteAsync(new CreateSourceTypeDto("Empresarial"));

        result.Id.Should().Be(3);
        result.Name.Should().Be("Empresarial");
        result.IsSystemSeed.Should().BeFalse();
        _uow.Verify(u => u.CommitAsync(default), Times.Once);
    }

    [Fact(DisplayName = "Deve lançar exceção para nome duplicado")]
    public async Task Execute_WithDuplicateName_ShouldThrow()
    {
        _repository.Setup(r => r.ExistsByNameAsync("Empresarial", default)).ReturnsAsync(true);

        var act = () => _sut.ExecuteAsync(new CreateSourceTypeDto("Empresarial"));

        await act.Should().ThrowAsync<DomainException>();
        _uow.Verify(u => u.CommitAsync(default), Times.Never);
    }

    [Theory(DisplayName = "Deve lançar exceção para nome inválido")]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null)]
    public async Task Execute_WithInvalidName_ShouldThrow(string? name)
    {
        var act = () => _sut.ExecuteAsync(new CreateSourceTypeDto(name!));

        await act.Should().ThrowAsync<DomainException>();
        _uow.Verify(u => u.CommitAsync(default), Times.Never);
    }
}
