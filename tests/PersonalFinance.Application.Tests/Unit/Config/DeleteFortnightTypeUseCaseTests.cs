using FluentAssertions;
using Moq;
using PersonalFinance.Application.UseCases.Config;
using PersonalFinance.Domain.Entities.Lookup;
using PersonalFinance.Domain.Exceptions;
using PersonalFinance.Domain.Interfaces.Repositories;
using Xunit;

namespace PersonalFinance.Application.Tests.Unit.Config;

public class DeleteFortnightTypeUseCaseTests
{
    private readonly Mock<IFortnightTypeRepository> _repository = new();
    private readonly Mock<IUnitOfWork>              _uow        = new();
    private readonly DeleteFortnightTypeUseCase     _sut;

    public DeleteFortnightTypeUseCaseTests()
    {
        _sut = new DeleteFortnightTypeUseCase(_repository.Object, _uow.Object);
    }

    [Fact(DisplayName = "Deve deletar item não-seed")]
    public async Task Execute_WithNonSeedId_ShouldDelete()
    {
        var entity = new FortnightType { Id = 3, Name = "Third" };
        _repository.Setup(r => r.GetByIdAsync(3, default)).ReturnsAsync(entity);

        await _sut.ExecuteAsync(3);

        _repository.Verify(r => r.DeleteAsync(3, default), Times.Once);
        _uow.Verify(u => u.CommitAsync(default), Times.Once);
    }

    [Theory(DisplayName = "Deve lançar exceção ao tentar deletar item seed")]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Execute_WithSeedId_ShouldThrow(int seedId)
    {
        var act = () => _sut.ExecuteAsync(seedId);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*sistema*");
        _uow.Verify(u => u.CommitAsync(default), Times.Never);
    }

    [Fact(DisplayName = "Deve lançar exceção se item não encontrado")]
    public async Task Execute_WithNotFoundId_ShouldThrow()
    {
        _repository.Setup(r => r.GetByIdAsync(99, default)).ReturnsAsync((FortnightType?)null);

        var act = () => _sut.ExecuteAsync(99);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*não encontrado*");
        _uow.Verify(u => u.CommitAsync(default), Times.Never);
    }
}
