using FluentAssertions;
using Moq;
using PersonalFinance.Application.DTOs.Admin;
using PersonalFinance.Application.UseCases.Config;
using PersonalFinance.Domain.Entities.Lookup;
using PersonalFinance.Domain.Exceptions;
using PersonalFinance.Domain.Interfaces.Repositories;
using Xunit;

namespace PersonalFinance.Application.Tests.Unit.Config;

public class UpdatePaymentStatusUseCaseTests
{
    private readonly Mock<IPaymentStatusRepository> _repository = new();
    private readonly Mock<IUnitOfWork>              _uow        = new();
    private readonly UpdatePaymentStatusUseCase     _sut;

    public UpdatePaymentStatusUseCaseTests()
    {
        _sut = new UpdatePaymentStatusUseCase(_repository.Object, _uow.Object);
    }

    [Fact(DisplayName = "Deve atualizar status com dados válidos")]
    public async Task Execute_WithValidData_ShouldUpdate()
    {
        var entity = new PaymentStatus { Id = 5, Name = "Antigo", Description = "Desc antiga" };
        _repository.Setup(r => r.GetByIdAsync(5, default)).ReturnsAsync(entity);
        _repository.Setup(r => r.ExistsByNameAsync("Parcelado", default)).ReturnsAsync(false);

        var result = await _sut.ExecuteAsync(new UpdatePaymentStatusDto(5, "Parcelado", "Pagamento em parcelas"));

        result.Id.Should().Be(5);
        result.Name.Should().Be("Parcelado");
        result.IsSystemSeed.Should().BeFalse();
        _repository.Verify(r => r.UpdateAsync(It.Is<PaymentStatus>(
            s => s.Name == "Parcelado" && s.Description == "Pagamento em parcelas"), default), Times.Once);
        _uow.Verify(u => u.CommitAsync(default), Times.Once);
    }

    [Theory(DisplayName = "Deve lançar exceção ao tentar atualizar item seed")]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task Execute_WithSeedId_ShouldThrow(int seedId)
    {
        var act = () => _sut.ExecuteAsync(new UpdatePaymentStatusDto(seedId, "Nome", "Desc"));

        await act.Should().ThrowAsync<DomainException>().WithMessage("*sistema*");
        _uow.Verify(u => u.CommitAsync(default), Times.Never);
    }

    [Theory(DisplayName = "Deve lançar exceção para nome inválido")]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null)]
    public async Task Execute_WithInvalidName_ShouldThrow(string? name)
    {
        var act = () => _sut.ExecuteAsync(new UpdatePaymentStatusDto(5, name!, "Desc"));

        await act.Should().ThrowAsync<DomainException>();
        _uow.Verify(u => u.CommitAsync(default), Times.Never);
    }

    [Fact(DisplayName = "Deve lançar exceção se nome já existe em outro item")]
    public async Task Execute_WithDuplicateName_ShouldThrow()
    {
        var entity = new PaymentStatus { Id = 5, Name = "Antigo", Description = "" };
        _repository.Setup(r => r.GetByIdAsync(5, default)).ReturnsAsync(entity);
        _repository.Setup(r => r.ExistsByNameAsync("Parcelado", default)).ReturnsAsync(true);

        var act = () => _sut.ExecuteAsync(new UpdatePaymentStatusDto(5, "Parcelado", "Desc"));

        await act.Should().ThrowAsync<DomainException>().WithMessage("*Parcelado*");
        _uow.Verify(u => u.CommitAsync(default), Times.Never);
    }

    [Fact(DisplayName = "Deve permitir salvar sem alterar o nome")]
    public async Task Execute_WithSameName_ShouldNotCheckDuplicate()
    {
        var entity = new PaymentStatus { Id = 5, Name = "Parcelado", Description = "Antiga" };
        _repository.Setup(r => r.GetByIdAsync(5, default)).ReturnsAsync(entity);

        var result = await _sut.ExecuteAsync(new UpdatePaymentStatusDto(5, "Parcelado", "Nova desc"));

        result.Should().NotBeNull();
        _repository.Verify(r => r.ExistsByNameAsync(It.IsAny<string>(), default), Times.Never);
        _uow.Verify(u => u.CommitAsync(default), Times.Once);
    }

    [Fact(DisplayName = "Deve lançar exceção se item não encontrado")]
    public async Task Execute_WithNotFoundId_ShouldThrow()
    {
        _repository.Setup(r => r.GetByIdAsync(99, default)).ReturnsAsync((PaymentStatus?)null);

        var act = () => _sut.ExecuteAsync(new UpdatePaymentStatusDto(99, "Nome", "Desc"));

        await act.Should().ThrowAsync<DomainException>().WithMessage("*não encontrado*");
    }
}
