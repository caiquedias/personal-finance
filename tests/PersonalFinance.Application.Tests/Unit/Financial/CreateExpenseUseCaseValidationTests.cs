using FluentAssertions;
using Moq;
using PersonalFinance.Application.DTOs.Financial;
using PersonalFinance.Application.Tests.Unit.Support;
using PersonalFinance.Application.UseCases.Financial.Expenses;
using PersonalFinance.Domain.Enums;
using PersonalFinance.Domain.Interfaces.Repositories;
using Xunit;

namespace PersonalFinance.Application.Tests.Unit.Financial;

/// <summary>Validação FluentValidation em CreateExpenseUseCase (#397).</summary>
public class CreateExpenseUseCaseValidationTests
{
    private readonly Mock<IExpenseRepository>  _expenseRepo  = new();
    private readonly Mock<IPeriodRepository>   _periodRepo   = new();
    private readonly Mock<ICategoryRepository> _categoryRepo = new();
    private readonly Mock<IUnitOfWork>         _uow          = new();

    [Fact(DisplayName = "Execute: validator reprova deve lançar ValidationException sem acessar repositório nem persistir")]
    public async Task Execute_WhenValidatorFails_ShouldThrowValidationExceptionWithoutPersisting()
    {
        var sut = UseCaseFactory.Create<CreateExpenseUseCase>(
            _expenseRepo.Object, _periodRepo.Object, _categoryRepo.Object, _uow.Object,
            TestValidators.Invalid<CreateExpenseDto>());

        var act = () => sut.ExecuteAsync(new CreateExpenseDto(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), SourceType.Personal, FortnightType.First,
            "x", 1m, new DateOnly(2026, 5, 10)));

        await act.Should().ThrowAsync<FluentValidation.ValidationException>();
        _expenseRepo.Invocations.Should().BeEmpty();
        _periodRepo.Invocations.Should().BeEmpty();
        _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
