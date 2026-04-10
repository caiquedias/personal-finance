using FluentAssertions;
using Moq;
using PersonalFinance.Application.DTOs.Admin;
using PersonalFinance.Application.UseCases.Config;
using PersonalFinance.Domain.Entities.Lookup;
using PersonalFinance.Domain.Exceptions;
using PersonalFinance.Domain.Interfaces.Repositories;
using Xunit;

namespace PersonalFinance.Application.Tests.Unit.Config;

public class UpdateFortnightTypeUseCaseTests
{
    private readonly Mock<IFortnightTypeRepository> _repository = new();
    private readonly Mock<IUnitOfWork>              _uow        = new();
    private readonly UpdateFortnightTypeUseCase     _sut;

    public UpdateFortnightTypeUseCaseTests()
    {
        _sut = new UpdateFortnightTypeUseCase(_repository.Object, _uow.Object);
    }

    [Fact(DisplayName = "Deve atualizar tipo com dados válidos")]
    public async Task Execute_WithValidData_ShouldUpdate()
    {
        var entity = new FortnightType { Id = 3, Name = "Antigo" };
        _repository.Setup(r => r.GetByIdAsync(3, default)).ReturnsAsync(entity);
        _repository.Setup(r => r.ExistsByNameAsync("Third", default)).ReturnsAsync(false);

        var result = await _sut.ExecuteAsync(new UpdateFortnightTypeDto(3, "Third"));

        result.Name.Should().Be("Third");
        _repository.Verify(r => r.UpdateAsync(It.Is<FortnightType>(s => s.Name == "Third"), default), Times.Once);
        _uow.Verify(u => u.CommitAsync(default), Times.Once);
    }

    [Theory(DisplayName = "Deve lançar exceção ao tentar atualizar item seed")]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Execute_WithSeedId_ShouldThrow(int seedId)
    {
        var act = () => _sut.ExecuteAsync(new UpdateFortnightTypeDto(seedId, "Nome"));

        await act.Should().ThrowAsync<DomainException>().WithMessage("*sistema*");
        _uow.Verify(u => u.CommitAsync(default), Times.Never);
    }

    [Fact(DisplayName = "Deve lançar exceção se nome já existe em outro item")]
    public async Task Execute_WithDuplicateName_ShouldThrow()
    {
        var entity = new FortnightType { Id = 3, Name = "Antigo" };
        _repository.Setup(r => r.GetByIdAsync(3, default)).ReturnsAsync(entity);
        _repository.Setup(r => r.ExistsByNameAsync("Third", default)).ReturnsAsync(true);

        var act = () => _sut.ExecuteAsync(new UpdateFortnightTypeDto(3, "Third"));

        await act.Should().ThrowAsync<DomainException>().WithMessage("*Third*");
        _uow.Verify(u => u.CommitAsync(default), Times.Never);
    }

    [Fact(DisplayName = "Deve lançar exceção se item não encontrado")]
    public async Task Execute_WithNotFoundId_ShouldThrow()
    {
        _repository.Setup(r => r.GetByIdAsync(99, default)).ReturnsAsync((FortnightType?)null);

        var act = () => _sut.ExecuteAsync(new UpdateFortnightTypeDto(99, "Nome"));

        await act.Should().ThrowAsync<DomainException>().WithMessage("*não encontrado*");
    }
}
