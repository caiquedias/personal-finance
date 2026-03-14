using FluentAssertions;
using Moq;
using PersonalFinance.Application.DTOs.Admin;
using PersonalFinance.Application.UseCases.Config;
using PersonalFinance.Domain.Entities.Lookup;
using PersonalFinance.Domain.Exceptions;
using PersonalFinance.Domain.Interfaces.Repositories;
using Xunit;

namespace PersonalFinance.Application.Tests.Unit.Config;

public class CreatePaymentStatusUseCaseTests
{
    private readonly Mock<IPaymentStatusRepository> _repository = new();
    private readonly Mock<IUnitOfWork>              _uow        = new();
    private readonly CreatePaymentStatusUseCase     _sut;

    public CreatePaymentStatusUseCaseTests()
    {
        _sut = new CreatePaymentStatusUseCase(_repository.Object, _uow.Object);
    }

    [Fact(DisplayName = "Deve criar status com dados válidos")]
    public async Task Execute_WithValidData_ShouldCreate()
    {
        _repository.Setup(r => r.ExistsByNameAsync("Parcelado", default)).ReturnsAsync(false);
        _repository.Setup(r => r.GetNextIdAsync(default)).ReturnsAsync(5);

        var result = await _sut.ExecuteAsync(
            new CreatePaymentStatusDto("Parcelado", "Pagamento em parcelas"));

        result.Id.Should().Be(5);
        result.Name.Should().Be("Parcelado");
        result.IsSystemSeed.Should().BeFalse();
        _repository.Verify(r => r.AddAsync(It.Is<PaymentStatus>(
            s => s.Id == 5 && s.Name == "Parcelado"), default), Times.Once);
        _uow.Verify(u => u.CommitAsync(default), Times.Once);
    }

    [Fact(DisplayName = "Deve lançar exceção para nome duplicado")]
    public async Task Execute_WithDuplicateName_ShouldThrow()
    {
        _repository.Setup(r => r.ExistsByNameAsync("Parcelado", default)).ReturnsAsync(true);

        var act = () => _sut.ExecuteAsync(
            new CreatePaymentStatusDto("Parcelado", "Desc"));

        await act.Should().ThrowAsync<DomainException>().WithMessage("*Parcelado*");
        _uow.Verify(u => u.CommitAsync(default), Times.Never);
    }

    [Theory(DisplayName = "Deve lançar exceção para nome inválido")]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null)]
    public async Task Execute_WithInvalidName_ShouldThrow(string? name)
    {
        var act = () => _sut.ExecuteAsync(new CreatePaymentStatusDto(name!, "Desc"));

        await act.Should().ThrowAsync<DomainException>();
        _uow.Verify(u => u.CommitAsync(default), Times.Never);
    }

    [Fact(DisplayName = "O ID gerado deve ser maior que o maior ID existente")]
    public async Task Execute_ShouldUseNextAvailableId()
    {
        _repository.Setup(r => r.ExistsByNameAsync(It.IsAny<string>(), default)).ReturnsAsync(false);
        _repository.Setup(r => r.GetNextIdAsync(default)).ReturnsAsync(10);

        var result = await _sut.ExecuteAsync(
            new CreatePaymentStatusDto("Novo Status", "Desc"));

        result.Id.Should().Be(10);
    }
}
