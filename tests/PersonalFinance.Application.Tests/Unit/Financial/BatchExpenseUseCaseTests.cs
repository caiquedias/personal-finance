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

public class BatchExpenseUseCaseTests
{
    private readonly Mock<IExpenseRepository> _repo = new();
    private readonly Mock<IUnitOfWork>        _uow  = new();

    private static Expense MakeExpense(Guid userId) =>
        Expense.Create(
            Guid.NewGuid(), userId, Guid.NewGuid(),
            SourceType.Personal, FortnightType.First, PaymentStatus.Pending,
            "Teste", 100m, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)), null, null);

    // ── DeleteExpensesBatchUseCase ────────────────────────────────────────────

    [Fact(DisplayName = "DeleteBatch: deve soft-deletar todos os itens encontrados")]
    public async Task DeleteBatch_ShouldSoftDeleteAll()
    {
        var userId = Guid.NewGuid();
        var ids    = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
        var expenses = ids.Select(_ => MakeExpense(userId)).ToList();

        _repo.Setup(r => r.GetByIdsAndUserAsync(ids, userId, default))
             .ReturnsAsync(expenses);

        var sut = new DeleteExpensesBatchUseCase(_repo.Object, _uow.Object);
        await sut.ExecuteAsync(new BatchExpenseActionDto(ids), userId);

        expenses.Should().AllSatisfy(e => e.IsDeleted.Should().BeTrue());
        _repo.Verify(r => r.UpdateRangeAsync(expenses, default), Times.Once);
        _uow.Verify(u => u.CommitAsync(default), Times.Once);
    }

    [Fact(DisplayName = "DeleteBatch: lista vazia deve lançar DomainException")]
    public async Task DeleteBatch_EmptyList_ShouldThrow()
    {
        var sut = new DeleteExpensesBatchUseCase(_repo.Object, _uow.Object);
        var act = () => sut.ExecuteAsync(new BatchExpenseActionDto([]), Guid.NewGuid());
        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact(DisplayName = "DeleteBatch: nenhuma despesa encontrada deve lançar DomainException")]
    public async Task DeleteBatch_NoneFound_ShouldThrow()
    {
        var ids = new List<Guid> { Guid.NewGuid() };
        _repo.Setup(r => r.GetByIdsAndUserAsync(ids, It.IsAny<Guid>(), default))
             .ReturnsAsync([]);

        var sut = new DeleteExpensesBatchUseCase(_repo.Object, _uow.Object);
        var act = () => sut.ExecuteAsync(new BatchExpenseActionDto(ids), Guid.NewGuid());
        await act.Should().ThrowAsync<DomainException>();
    }

    // ── PayExpensesBatchUseCase ───────────────────────────────────────────────

    [Fact(DisplayName = "PayBatch: deve marcar todos como Paid")]
    public async Task PayBatch_ShouldMarkAllAsPaid()
    {
        var userId   = Guid.NewGuid();
        var ids      = new List<Guid> { Guid.NewGuid() };
        var expenses = ids.Select(_ => MakeExpense(userId)).ToList();

        _repo.Setup(r => r.GetByIdsAndUserAsync(ids, userId, default))
             .ReturnsAsync(expenses);

        var sut = new PayExpensesBatchUseCase(_repo.Object, _uow.Object);
        await sut.ExecuteAsync(new BatchExpenseActionDto(ids), userId);

        expenses.Should().AllSatisfy(e => e.PaymentStatus.Should().Be(PaymentStatus.Paid));
        _uow.Verify(u => u.CommitAsync(default), Times.Once);
    }

    [Fact(DisplayName = "PayBatch: lista vazia deve lançar DomainException")]
    public async Task PayBatch_EmptyList_ShouldThrow()
    {
        var sut = new PayExpensesBatchUseCase(_repo.Object, _uow.Object);
        var act = () => sut.ExecuteAsync(new BatchExpenseActionDto([]), Guid.NewGuid());
        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact(DisplayName = "PayBatch: nenhuma despesa encontrada deve lançar DomainException")]
    public async Task PayBatch_NoneFound_ShouldThrow()
    {
        var ids = new List<Guid> { Guid.NewGuid() };
        _repo.Setup(r => r.GetByIdsAndUserAsync(ids, It.IsAny<Guid>(), default))
             .ReturnsAsync([]);

        var sut = new PayExpensesBatchUseCase(_repo.Object, _uow.Object);
        var act = () => sut.ExecuteAsync(new BatchExpenseActionDto(ids), Guid.NewGuid());
        await act.Should().ThrowAsync<DomainException>();
    }

    // ── CancelExpensesBatchUseCase ────────────────────────────────────────────

    [Fact(DisplayName = "CancelBatch: deve marcar todos como Cancelled")]
    public async Task CancelBatch_ShouldMarkAllAsCancelled()
    {
        var userId   = Guid.NewGuid();
        var ids      = new List<Guid> { Guid.NewGuid() };
        var expenses = ids.Select(_ => MakeExpense(userId)).ToList();

        _repo.Setup(r => r.GetByIdsAndUserAsync(ids, userId, default))
             .ReturnsAsync(expenses);

        var sut = new CancelExpensesBatchUseCase(_repo.Object, _uow.Object);
        await sut.ExecuteAsync(new BatchExpenseActionDto(ids), userId);

        expenses.Should().AllSatisfy(e => e.PaymentStatus.Should().Be(PaymentStatus.Cancelled));
        _uow.Verify(u => u.CommitAsync(default), Times.Once);
    }

    [Fact(DisplayName = "CancelBatch: lista vazia deve lançar DomainException")]
    public async Task CancelBatch_EmptyList_ShouldThrow()
    {
        var sut = new CancelExpensesBatchUseCase(_repo.Object, _uow.Object);
        var act = () => sut.ExecuteAsync(new BatchExpenseActionDto([]), Guid.NewGuid());
        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact(DisplayName = "CancelBatch: nenhuma despesa encontrada deve lançar DomainException")]
    public async Task CancelBatch_NoneFound_ShouldThrow()
    {
        var ids = new List<Guid> { Guid.NewGuid() };
        _repo.Setup(r => r.GetByIdsAndUserAsync(ids, It.IsAny<Guid>(), default))
             .ReturnsAsync([]);

        var sut = new CancelExpensesBatchUseCase(_repo.Object, _uow.Object);
        var act = () => sut.ExecuteAsync(new BatchExpenseActionDto(ids), Guid.NewGuid());
        await act.Should().ThrowAsync<DomainException>();
    }
}
