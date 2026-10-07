using FluentAssertions;
using Moq;
using PersonalFinance.Application.DTOs.Financial;
using PersonalFinance.Application.Tests.Unit.Support;
using PersonalFinance.Application.UseCases.Financial.Incomes;
using PersonalFinance.Domain.Enums;
using PersonalFinance.Domain.Interfaces.Repositories;
using Xunit;

namespace PersonalFinance.Application.Tests.Unit.Financial;

/// <summary>Validação FluentValidation em CreateIncomeUseCase (#397).</summary>
public class CreateIncomeUseCaseValidationTests
{
    private readonly Mock<IIncomeRepository> _incomeRepo = new();
    private readonly Mock<IPeriodRepository> _periodRepo = new();
    private readonly Mock<IUnitOfWork>       _uow        = new();

    [Fact(DisplayName = "Execute: validator reprova deve lançar ValidationException sem acessar repositório nem persistir")]
    public async Task Execute_WhenValidatorFails_ShouldThrowValidationExceptionWithoutPersisting()
    {
        var sut = UseCaseFactory.Create<CreateIncomeUseCase>(
            _incomeRepo.Object, _periodRepo.Object, _uow.Object,
            TestValidators.Invalid<CreateIncomeDto>());

        var act = () => sut.ExecuteAsync(new CreateIncomeDto(
            Guid.NewGuid(), Guid.NewGuid(), FortnightType.First, "x", 1m, new DateOnly(2026, 5, 5)));

        await act.Should().ThrowAsync<FluentValidation.ValidationException>();
        _incomeRepo.Invocations.Should().BeEmpty();
        _periodRepo.Invocations.Should().BeEmpty();
        _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
