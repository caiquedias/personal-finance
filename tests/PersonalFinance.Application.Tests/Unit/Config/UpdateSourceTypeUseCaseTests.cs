using FluentAssertions;
using Moq;
using PersonalFinance.Application.DTOs.Admin;
using PersonalFinance.Application.UseCases.Config;
using PersonalFinance.Domain.Entities.Lookup;
using PersonalFinance.Domain.Exceptions;
using PersonalFinance.Domain.Interfaces.Repositories;
using Xunit;

namespace PersonalFinance.Application.Tests.Unit.Config;

public class UpdateSourceTypeUseCaseTests
{
    private readonly Mock<ISourceTypeRepository> _repository = new();
    private readonly Mock<IUnitOfWork>           _uow        = new();
    private readonly UpdateSourceTypeUseCase     _sut;

    public UpdateSourceTypeUseCaseTests()
    {
        _sut = new UpdateSourceTypeUseCase(_repository.Object, _uow.Object);
    }

    [Fact(DisplayName = "Deve atualizar tipo com dados válidos")]
    public async Task Execute_WithValidData_ShouldUpdate()
    {
        var entity = new SourceType { Id = 3, Name = "Antigo" };
        _repository.Setup(r => r.GetByIdAsync(3, default)).ReturnsAsync(entity);
        _repository.Setup(r => r.ExistsByNameAsync("Empresarial", default)).ReturnsAsync(false);

        var result = await _sut.ExecuteAsync(new UpdateSourceTypeDto(3, "Empresarial"));

        result.Name.Should().Be("Empresarial");
        _repository.Verify(r => r.UpdateAsync(It.Is<SourceType>(s => s.Name == "Empresarial"), default), Times.Once);
        _uow.Verify(u => u.CommitAsync(default), Times.Once);
    }

    [Theory(DisplayName = "Deve lançar exceção ao tentar atualizar item seed")]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Execute_WithSeedId_ShouldThrow(int seedId)
    {
        var act = () => _sut.ExecuteAsync(new UpdateSourceTypeDto(seedId, "Nome"));

        await act.Should().ThrowAsync<DomainException>().WithMessage("*sistema*");
        _uow.Verify(u => u.CommitAsync(default), Times.Never);
    }

    [Fact(DisplayName = "Deve lançar exceção se nome já existe em outro item")]
    public async Task Execute_WithDuplicateName_ShouldThrow()
    {
        var entity = new SourceType { Id = 3, Name = "Antigo" };
        _repository.Setup(r => r.GetByIdAsync(3, default)).ReturnsAsync(entity);
        _repository.Setup(r => r.ExistsByNameAsync("Empresarial", default)).ReturnsAsync(true);

        var act = () => _sut.ExecuteAsync(new UpdateSourceTypeDto(3, "Empresarial"));

        await act.Should().ThrowAsync<DomainException>().WithMessage("*Empresarial*");
        _uow.Verify(u => u.CommitAsync(default), Times.Never);
    }

    [Fact(DisplayName = "Deve lançar exceção se item não encontrado")]
    public async Task Execute_WithNotFoundId_ShouldThrow()
    {
        _repository.Setup(r => r.GetByIdAsync(99, default)).ReturnsAsync((SourceType?)null);

        var act = () => _sut.ExecuteAsync(new UpdateSourceTypeDto(99, "Nome"));

        await act.Should().ThrowAsync<DomainException>().WithMessage("*não encontrado*");
    }
}
