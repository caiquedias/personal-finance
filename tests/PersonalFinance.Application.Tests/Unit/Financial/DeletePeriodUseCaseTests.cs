using FluentAssertions;
using Moq;
using PersonalFinance.Application.UseCases.Financial.Periods;
using PersonalFinance.Domain.Entities.Financial;
using PersonalFinance.Domain.Exceptions;
using PersonalFinance.Domain.Interfaces.Repositories;
using Xunit;

namespace PersonalFinance.Application.Tests.Unit.Financial;

public class DeletePeriodUseCaseTests
{
    private readonly Mock<IPeriodRepository>  _periodRepo  = new();
    private readonly Mock<IExpenseRepository> _expenseRepo = new();
    private readonly Mock<IIncomeRepository>  _incomeRepo  = new();
    private readonly Mock<IUnitOfWork>        _uow         = new();
    private readonly DeletePeriodUseCase      _sut;

    private static readonly Guid UserId   = Guid.NewGuid();
    private static readonly Guid PeriodId = Guid.NewGuid();

    public DeletePeriodUseCaseTests()
    {
        _sut = new DeletePeriodUseCase(
            _periodRepo.Object, _expenseRepo.Object,
            _incomeRepo.Object, _uow.Object);
    }

    [Fact(DisplayName = "Deve fazer soft delete de período sem lançamentos")]
    public async Task Execute_EmptyPeriod_ShouldSoftDelete()
    {
        var period = Period.Create(UserId, 2026, 4);
        _periodRepo.Setup(r => r.GetByIdAndUserAsync(PeriodId, UserId, default))
                   .ReturnsAsync(period);
        _expenseRepo.Setup(r => r.GetByPeriodAsync(PeriodId, UserId, default))
                    .ReturnsAsync(Enumerable.Empty<Expense>());
        _incomeRepo.Setup(r => r.GetByPeriodAsync(PeriodId, UserId, default))
                   .ReturnsAsync(Enumerable.Empty<Income>());

        await _sut.ExecuteAsync(PeriodId, UserId);

        period.IsDeleted.Should().BeTrue();
        _uow.Verify(u => u.CommitAsync(default), Times.Once);
    }

    [Fact(DisplayName = "Deve bloquear exclusão de período com despesas vinculadas")]
    public async Task Execute_PeriodWithExpenses_ShouldThrow()
    {
        var period  = Period.Create(UserId, 2026, 4);
        var expense = Expense.Create(PeriodId, UserId, Guid.NewGuid(),
            Domain.Enums.SourceType.Personal, Domain.Enums.FortnightType.First,
            Domain.Enums.PaymentStatus.Pending, "Internet", 110m,
            new DateOnly(2026,4,20), null, null);

        _periodRepo.Setup(r => r.GetByIdAndUserAsync(PeriodId, UserId, default))
                   .ReturnsAsync(period);
        _expenseRepo.Setup(r => r.GetByPeriodAsync(PeriodId, UserId, default))
                    .ReturnsAsync(new[] { expense });

        var act = () => _sut.ExecuteAsync(PeriodId, UserId);

        await act.Should().ThrowAsync<DomainException>()
                 .WithMessage("*despesas vinculadas*");
        period.IsDeleted.Should().BeFalse();
        _uow.Verify(u => u.CommitAsync(default), Times.Never);
    }

    [Fact(DisplayName = "Deve bloquear exclusão de período com receitas vinculadas")]
    public async Task Execute_PeriodWithIncomes_ShouldThrow()
    {
        var period = Period.Create(UserId, 2026, 4);
        var income = Income.Create(PeriodId, UserId,
            Domain.Enums.FortnightType.First,
            "Salário", 5000m, new DateOnly(2026,4,1), null);

        _periodRepo.Setup(r => r.GetByIdAndUserAsync(PeriodId, UserId, default))
                   .ReturnsAsync(period);
        _expenseRepo.Setup(r => r.GetByPeriodAsync(PeriodId, UserId, default))
                    .ReturnsAsync(Enumerable.Empty<Expense>());
        _incomeRepo.Setup(r => r.GetByPeriodAsync(PeriodId, UserId, default))
                   .ReturnsAsync(new[] { income });

        var act = () => _sut.ExecuteAsync(PeriodId, UserId);

        await act.Should().ThrowAsync<DomainException>()
                 .WithMessage("*receitas vinculadas*");
        _uow.Verify(u => u.CommitAsync(default), Times.Never);
    }

    [Fact(DisplayName = "Deve lançar exceção para período não encontrado")]
    public async Task Execute_NotFound_ShouldThrow()
    {
        _periodRepo.Setup(r => r.GetByIdAndUserAsync(PeriodId, UserId, default))
                   .ReturnsAsync((Period?)null);

        var act = () => _sut.ExecuteAsync(PeriodId, UserId);

        await act.Should().ThrowAsync<DomainException>()
                 .WithMessage("*Período não encontrado*");
        _uow.Verify(u => u.CommitAsync(default), Times.Never);
    }
}
