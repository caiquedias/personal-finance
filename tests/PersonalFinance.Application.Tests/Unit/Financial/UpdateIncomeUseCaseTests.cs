using FluentAssertions;
using Moq;
using PersonalFinance.Application.DTOs.Financial;
using PersonalFinance.Application.UseCases.Financial.Incomes;
using PersonalFinance.Domain.Entities.Financial;
using PersonalFinance.Domain.Enums;
using PersonalFinance.Domain.Exceptions;
using PersonalFinance.Domain.Interfaces.Repositories;
using Xunit;

namespace PersonalFinance.Application.Tests.Unit.Financial;

public class UpdateIncomeUseCaseTests
{
    private readonly Mock<IIncomeRepository> _incomeRepo = new();
    private readonly Mock<IUnitOfWork>       _uow        = new();
    private readonly UpdateIncomeUseCase     _sut;

    private static readonly Guid UserId   = Guid.NewGuid();
    private static readonly Guid IncomeId = Guid.NewGuid();

    public UpdateIncomeUseCaseTests() =>
        _sut = new UpdateIncomeUseCase(_incomeRepo.Object, _uow.Object);

    private static Income FakeIncome() => Income.Create(
        Guid.NewGuid(), UserId, FortnightType.First,
        "Original", 100m, new DateOnly(2026, 4, 10), null);

    private static UpdateIncomeDto ValidDto() => new(
        IncomeId, UserId, FortnightType.Second,
        "Adiantamento MDS", 5500m, new DateOnly(2026, 4, 15), "Obs");

    [Fact(DisplayName = "Deve atualizar receita existente")]
    public async Task Execute_WithExistingIncome_ShouldUpdate()
    {
        var income = FakeIncome();
        _incomeRepo.Setup(r => r.GetByIdAndUserAsync(IncomeId, UserId, default))
                   .ReturnsAsync(income);

        await _sut.ExecuteAsync(ValidDto());

        income.Description.Should().Be("Adiantamento MDS");
        income.Amount.Should().Be(5500m);
        income.FortnightType.Should().Be(FortnightType.Second);
        income.ReceivedAt.Should().Be(new DateOnly(2026, 4, 15));
        income.Notes.Should().Be("Obs");
        _incomeRepo.Verify(r => r.UpdateAsync(income, default), Times.Once);
        _uow.Verify(u => u.CommitAsync(default), Times.Once);
    }

    [Fact(DisplayName = "Deve lançar exceção se receita não for encontrada ou não pertencer ao usuário")]
    public async Task Execute_WithUnauthorizedOrNotFoundIncome_ShouldThrow()
    {
        // GetByIdAndUserAsync filtra por UserId — receita de outro usuário ou inexistente retorna null
        _incomeRepo.Setup(r => r.GetByIdAndUserAsync(IncomeId, UserId, default))
                   .ReturnsAsync((Income?)null);

        var act = () => _sut.ExecuteAsync(ValidDto());

        await act.Should().ThrowAsync<DomainException>()
                 .WithMessage("*receita*");
        _incomeRepo.Verify(r => r.UpdateAsync(It.IsAny<Income>(), default), Times.Never);
        _uow.Verify(u => u.CommitAsync(default), Times.Never);
    }

    [Theory(DisplayName = "Deve lançar exceção para Amount inválido")]
    [InlineData(0)]
    [InlineData(-50)]
    public async Task Execute_WithInvalidAmount_ShouldThrow(decimal amount)
    {
        var income = FakeIncome();
        _incomeRepo.Setup(r => r.GetByIdAndUserAsync(IncomeId, UserId, default))
                   .ReturnsAsync(income);

        var act = () => _sut.ExecuteAsync(ValidDto() with { Amount = amount });

        await act.Should().ThrowAsync<Exception>();
        _uow.Verify(u => u.CommitAsync(default), Times.Never);
    }

    [Fact(DisplayName = "Deve lançar exceção para descrição vazia")]
    public async Task Execute_WithEmptyDescription_ShouldThrow()
    {
        var income = FakeIncome();
        _incomeRepo.Setup(r => r.GetByIdAndUserAsync(IncomeId, UserId, default))
                   .ReturnsAsync(income);

        var act = () => _sut.ExecuteAsync(ValidDto() with { Description = "" });

        await act.Should().ThrowAsync<Exception>();
        _uow.Verify(u => u.CommitAsync(default), Times.Never);
    }

    [Fact(DisplayName = "Execute: validator reprova deve lançar ValidationException sem acessar repositório nem persistir")]
    public async Task Execute_WhenValidatorFails_ShouldThrowValidationExceptionWithoutPersisting()
    {
        var sut = PersonalFinance.Application.Tests.Unit.Support.UseCaseFactory.Create<UpdateIncomeUseCase>(
            _incomeRepo.Object, _uow.Object,
            PersonalFinance.Application.Tests.Unit.Support.TestValidators.Invalid<UpdateIncomeDto>());

        var act = () => sut.ExecuteAsync(new UpdateIncomeDto(
            Guid.NewGuid(), Guid.NewGuid(), FortnightType.First, "x", 1m, new DateOnly(2026, 5, 5), null));

        await act.Should().ThrowAsync<FluentValidation.ValidationException>();
        _incomeRepo.Invocations.Should().BeEmpty();
        _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
