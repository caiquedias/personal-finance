using FluentAssertions;
using Moq;
using PersonalFinance.Application.DTOs.Financial;
using PersonalFinance.Application.UseCases.Financial.Expenses;
using PersonalFinance.Domain.Enums;
using PersonalFinance.Domain.Exceptions;
using PersonalFinance.Domain.Interfaces.Repositories;
using Xunit;

namespace PersonalFinance.Application.Tests.Unit.Financial;

public class CreateExpensesBatchUseCaseTests
{
    private readonly Mock<IExpenseRepository>  _expenseRepo  = new();
    private readonly Mock<IPeriodRepository>   _periodRepo   = new();
    private readonly Mock<ICategoryRepository> _categoryRepo = new();
    private readonly Mock<IUnitOfWork>         _uow          = new();

    private static BatchExpenseItemDto MakeItem(Guid categoryId) =>
        new(categoryId, SourceType.Personal, FortnightType.First,
            "Despesa Teste", 100m, DateOnly.FromDateTime(DateTime.UtcNow));

    [Fact(DisplayName = "CreateBatch: deve criar N despesas e retornar lista com mesmo tamanho")]
    public async Task CreateBatch_ShouldCreateAllExpensesAndReturnList()
    {
        var userId   = Guid.NewGuid();
        var periodId = Guid.NewGuid();
        var catId1   = Guid.NewGuid();
        var catId2   = Guid.NewGuid();

        _periodRepo.Setup(r => r.ExistsByIdAndUserAsync(periodId, userId, default))
                   .ReturnsAsync(true);
        _categoryRepo.Setup(r => r.IsAccessibleByUserAsync(catId1, userId, default))
                     .ReturnsAsync(true);
        _categoryRepo.Setup(r => r.IsAccessibleByUserAsync(catId2, userId, default))
                     .ReturnsAsync(true);

        var dto = new CreateExpensesBatchDto(periodId, userId, [MakeItem(catId1), MakeItem(catId2)]);
        var sut = new CreateExpensesBatchUseCase(
            _expenseRepo.Object, _periodRepo.Object, _categoryRepo.Object, _uow.Object);

        var result = await sut.ExecuteAsync(dto);

        result.Should().HaveCount(2);
        _uow.Verify(u => u.CommitAsync(default), Times.Once);
    }

    [Fact(DisplayName = "CreateBatch: lista vazia deve lançar DomainException sem commit")]
    public async Task CreateBatch_EmptyList_ShouldThrowWithoutCommit()
    {
        var dto = new CreateExpensesBatchDto(Guid.NewGuid(), Guid.NewGuid(), []);
        var sut = new CreateExpensesBatchUseCase(
            _expenseRepo.Object, _periodRepo.Object, _categoryRepo.Object, _uow.Object);

        var act = () => sut.ExecuteAsync(dto);
        await act.Should().ThrowAsync<DomainException>();
        _uow.Verify(u => u.CommitAsync(default), Times.Never);
    }

    [Fact(DisplayName = "CreateBatch: período inválido deve lançar DomainException sem commit")]
    public async Task CreateBatch_InvalidPeriod_ShouldThrowWithoutCommit()
    {
        var userId   = Guid.NewGuid();
        var periodId = Guid.NewGuid();

        _periodRepo.Setup(r => r.ExistsByIdAndUserAsync(periodId, userId, default))
                   .ReturnsAsync(false);

        var dto = new CreateExpensesBatchDto(periodId, userId, [MakeItem(Guid.NewGuid())]);
        var sut = new CreateExpensesBatchUseCase(
            _expenseRepo.Object, _periodRepo.Object, _categoryRepo.Object, _uow.Object);

        var act = () => sut.ExecuteAsync(dto);
        await act.Should().ThrowAsync<DomainException>();
        _uow.Verify(u => u.CommitAsync(default), Times.Never);
    }

    [Fact(DisplayName = "CreateBatch: categoria inválida deve lançar DomainException sem commit")]
    public async Task CreateBatch_InvalidCategory_ShouldThrowWithoutCommit()
    {
        var userId   = Guid.NewGuid();
        var periodId = Guid.NewGuid();
        var catId    = Guid.NewGuid();

        _periodRepo.Setup(r => r.ExistsByIdAndUserAsync(periodId, userId, default))
                   .ReturnsAsync(true);
        _categoryRepo.Setup(r => r.IsAccessibleByUserAsync(catId, userId, default))
                     .ReturnsAsync(false);

        var dto = new CreateExpensesBatchDto(periodId, userId, [MakeItem(catId)]);
        var sut = new CreateExpensesBatchUseCase(
            _expenseRepo.Object, _periodRepo.Object, _categoryRepo.Object, _uow.Object);

        var act = () => sut.ExecuteAsync(dto);
        await act.Should().ThrowAsync<DomainException>();
        _uow.Verify(u => u.CommitAsync(default), Times.Never);
    }
}
