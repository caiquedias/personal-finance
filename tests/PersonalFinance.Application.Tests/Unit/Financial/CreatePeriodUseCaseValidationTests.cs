using FluentAssertions;
using Moq;
using PersonalFinance.Application.DTOs.Financial;
using PersonalFinance.Application.Tests.Unit.Support;
using PersonalFinance.Application.UseCases.Financial.Periods;
using PersonalFinance.Domain.Interfaces.Repositories;
using Xunit;

namespace PersonalFinance.Application.Tests.Unit.Financial;

/// <summary>Validação FluentValidation em CreatePeriodUseCase (#397).</summary>
public class CreatePeriodUseCaseValidationTests
{
    private readonly Mock<IPeriodRepository> _periodRepo = new();
    private readonly Mock<IUnitOfWork>       _uow        = new();

    [Fact(DisplayName = "Execute: validator reprova deve lançar ValidationException sem acessar repositório nem persistir")]
    public async Task Execute_WhenValidatorFails_ShouldThrowValidationExceptionWithoutPersisting()
    {
        var sut = UseCaseFactory.Create<CreatePeriodUseCase>(
            _periodRepo.Object, _uow.Object, TestValidators.Invalid<CreatePeriodDto>());

        var act = () => sut.ExecuteAsync(new CreatePeriodDto(Guid.NewGuid(), 2026, 5));

        await act.Should().ThrowAsync<FluentValidation.ValidationException>();
        _periodRepo.Invocations.Should().BeEmpty();
        _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
