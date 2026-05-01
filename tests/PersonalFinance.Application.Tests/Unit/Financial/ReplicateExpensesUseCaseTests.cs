using FluentAssertions;
using Moq;
using PersonalFinance.Application.DTOs.Financial;
using PersonalFinance.Application.UseCases.Financial.Expenses;
using PersonalFinance.Domain.Entities.Financial;
using PersonalFinance.Domain.Enums;
using PersonalFinance.Domain.Exceptions;
using PersonalFinance.Domain.Interfaces.Repositories;
using Xunit;

namespace PersonalFinance.Application.Tests.Unit.Financial;

public class ReplicateExpensesUseCaseTests
{
    private readonly Mock<IExpenseRepository> _expRepo  = new();
    private readonly Mock<IPeriodRepository>  _perRepo  = new();
    private readonly Mock<IUnitOfWork>        _uow      = new();

    private static Period MakePeriod(Guid userId, int year = 2025, int month = 5)
        => Period.Create(userId, year, month);

    private static Expense MakeRecurringExpense(Guid userId, int day = 10)
        => Expense.Create(
            Guid.NewGuid(), userId, Guid.NewGuid(),
            SourceType.Personal, FortnightType.First, PaymentStatus.Pending,
            "Aluguel", 1500m, new DateOnly(2025, 4, day), null, null,
            isRecurring: true);

    [Fact(DisplayName = "Deve replicar despesas com DueDate ajustado para o período destino")]
    public async Task ShouldReplicateWithAdjustedDueDate()
    {
        var userId   = Guid.NewGuid();
        var period   = MakePeriod(userId, 2025, 5);
        var source   = MakeRecurringExpense(userId, day: 10);
        var ids      = new List<Guid> { source.Id };

        _perRepo.Setup(r => r.GetByIdAndUserAsync(period.Id, userId, default))
                .ReturnsAsync(period);
        _expRepo.Setup(r => r.GetByIdsAndUserAsync(ids, userId, default))
                .ReturnsAsync([source]);
        _expRepo.Setup(r => r.HasReplicatedExpenseAsync(source.Id, period.Id, default))
                .ReturnsAsync(false);

        List<Expense>? captured = null;
        _expRepo.Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<Expense>>(), default))
                .Callback<IEnumerable<Expense>, CancellationToken>((e, _) => captured = e.ToList())
                .Returns(Task.CompletedTask);

        var sut = new ReplicateExpensesUseCase(_expRepo.Object, _perRepo.Object, _uow.Object);
        await sut.ExecuteAsync(new ReplicateExpensesDto(userId, period.Id, ids));

        captured.Should().HaveCount(1);
        captured![0].DueDate.Should().Be(new DateOnly(2025, 5, 10));
        captured[0].PaymentStatus.Should().Be(PaymentStatus.Pending);
        captured[0].PaymentDate.Should().BeNull();
        captured[0].SourceExpenseId.Should().Be(source.Id);
        _uow.Verify(u => u.CommitAsync(default), Times.Once);
    }

    [Fact(DisplayName = "Deve ignorar despesas já replicadas no período (idempotência)")]
    public async Task ShouldSkipAlreadyReplicatedExpenses()
    {
        var userId = Guid.NewGuid();
        var period = MakePeriod(userId, 2025, 5);
        var source = MakeRecurringExpense(userId);
        var ids    = new List<Guid> { source.Id };

        _perRepo.Setup(r => r.GetByIdAndUserAsync(period.Id, userId, default))
                .ReturnsAsync(period);
        _expRepo.Setup(r => r.GetByIdsAndUserAsync(ids, userId, default))
                .ReturnsAsync([source]);
        _expRepo.Setup(r => r.HasReplicatedExpenseAsync(source.Id, period.Id, default))
                .ReturnsAsync(true); // já replicada

        var sut = new ReplicateExpensesUseCase(_expRepo.Object, _perRepo.Object, _uow.Object);
        await sut.ExecuteAsync(new ReplicateExpensesDto(userId, period.Id, ids));

        _expRepo.Verify(r => r.AddRangeAsync(It.IsAny<IEnumerable<Expense>>(), default), Times.Never);
        _uow.Verify(u => u.CommitAsync(default), Times.Never);
    }

    [Fact(DisplayName = "Lista vazia deve lançar DomainException")]
    public async Task EmptyList_ShouldThrow()
    {
        var sut = new ReplicateExpensesUseCase(_expRepo.Object, _perRepo.Object, _uow.Object);
        var act = () => sut.ExecuteAsync(new ReplicateExpensesDto(Guid.NewGuid(), Guid.NewGuid(), []));
        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact(DisplayName = "Período não encontrado deve lançar DomainException")]
    public async Task PeriodNotFound_ShouldThrow()
    {
        var ids = new List<Guid> { Guid.NewGuid() };
        _perRepo.Setup(r => r.GetByIdAndUserAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), default))
                .ReturnsAsync((Period?)null);

        var sut = new ReplicateExpensesUseCase(_expRepo.Object, _perRepo.Object, _uow.Object);
        var act = () => sut.ExecuteAsync(new ReplicateExpensesDto(Guid.NewGuid(), Guid.NewGuid(), ids));
        await act.Should().ThrowAsync<DomainException>();
    }
}
