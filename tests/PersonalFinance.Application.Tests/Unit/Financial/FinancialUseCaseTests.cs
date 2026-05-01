using FluentAssertions;
using Moq;
using PersonalFinance.Application.DTOs.Financial;
using PersonalFinance.Application.UseCases.Financial.Expenses;
using PersonalFinance.Application.UseCases.Financial.Incomes;
using PersonalFinance.Application.UseCases.Financial.Periods;
using PersonalFinance.Domain.Entities.Financial;
using PersonalFinance.Domain.Enums;
using PersonalFinance.Domain.Exceptions;
using PersonalFinance.Domain.Interfaces.Repositories;
using Xunit;

namespace PersonalFinance.Application.Tests.Unit.Financial;

// ══════════════════════════════════════════════════════════════════════════════
// PERIOD
// ══════════════════════════════════════════════════════════════════════════════

public class CreatePeriodUseCaseTests
{
    private readonly Mock<IPeriodRepository> _periodRepo = new();
    private readonly Mock<IUnitOfWork>       _uow        = new();
    private readonly CreatePeriodUseCase     _sut;

    private static readonly Guid UserId = Guid.NewGuid();

    public CreatePeriodUseCaseTests() =>
        _sut = new CreatePeriodUseCase(_periodRepo.Object, _uow.Object);

    [Fact(DisplayName = "Deve criar período com dados válidos")]
    public async Task Execute_WithValidData_ShouldCreatePeriod()
    {
        _periodRepo.Setup(r => r.GetByUserYearMonthAsync(UserId, 2026, 4, default))
                   .ReturnsAsync((Period?)null);

        var result = await _sut.ExecuteAsync(new CreatePeriodDto(UserId, 2026, 4));

        result.Year.Should().Be(2026);
        result.Month.Should().Be(4);
        _periodRepo.Verify(r => r.AddAsync(It.IsAny<Period>(), default), Times.Once);
        _uow.Verify(u => u.CommitAsync(default), Times.Once);
    }

    [Fact(DisplayName = "Deve lançar exceção para período já existente no mês")]
    public async Task Execute_WithDuplicatePeriod_ShouldThrow()
    {
        var existing = Period.Create(UserId, 2026, 4);
        _periodRepo.Setup(r => r.GetByUserYearMonthAsync(UserId, 2026, 4, default))
                   .ReturnsAsync(existing);

        var act = () => _sut.ExecuteAsync(new CreatePeriodDto(UserId, 2026, 4));

        await act.Should().ThrowAsync<DomainException>()
                 .WithMessage("*período*");
        _uow.Verify(u => u.CommitAsync(default), Times.Never);
    }

    [Theory(DisplayName = "Deve lançar exceção para mês inválido")]
    [InlineData(0)]
    [InlineData(13)]
    public async Task Execute_WithInvalidMonth_ShouldThrow(int month)
    {
        var act = () => _sut.ExecuteAsync(new CreatePeriodDto(UserId, 2026, month));
        await act.Should().ThrowAsync<Exception>();
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// EXPENSE
// ══════════════════════════════════════════════════════════════════════════════

public class CreateExpenseUseCaseTests
{
    private readonly Mock<IExpenseRepository>  _expenseRepo  = new();
    private readonly Mock<IPeriodRepository>   _periodRepo   = new();
    private readonly Mock<ICategoryRepository> _categoryRepo = new();
    private readonly Mock<IUnitOfWork>         _uow          = new();
    private readonly CreateExpenseUseCase      _sut;

    private static readonly Guid UserId     = Guid.NewGuid();
    private static readonly Guid PeriodId   = Guid.NewGuid();
    private static readonly Guid CategoryId = Guid.NewGuid();

    public CreateExpenseUseCaseTests() =>
        _sut = new CreateExpenseUseCase(
            _expenseRepo.Object, _periodRepo.Object,
            _categoryRepo.Object, _uow.Object);

    private CreateExpenseDto ValidDto() => new(
        PeriodId:      PeriodId,
        UserId:        UserId,
        CategoryId:    CategoryId,
        SourceType:    SourceType.Personal,
        FortnightType: FortnightType.First,
        Description:   "Aluguel",
        Amount:        1200m,
        DueDate:       new DateOnly(2026, 4, 10)
    );

    private void SetupValidDependencies()
    {
        _periodRepo.Setup(r => r.ExistsByIdAndUserAsync(PeriodId, UserId, default))
                   .ReturnsAsync(true);
        _categoryRepo.Setup(r => r.IsAccessibleByUserAsync(CategoryId, UserId, default))
                     .ReturnsAsync(true);
    }

    [Fact(DisplayName = "Deve criar despesa com dados válidos")]
    public async Task Execute_WithValidData_ShouldCreateExpense()
    {
        SetupValidDependencies();

        var result = await _sut.ExecuteAsync(ValidDto());

        result.Description.Should().Be("Aluguel");
        result.Amount.Should().Be(1200m);
        _expenseRepo.Verify(r => r.AddAsync(It.IsAny<Expense>(), default), Times.Once);
        _uow.Verify(u => u.CommitAsync(default), Times.Once);
    }

    [Fact(DisplayName = "Deve lançar exceção se período não pertencer ao usuário")]
    public async Task Execute_WithUnauthorizedPeriod_ShouldThrow()
    {
        _periodRepo.Setup(r => r.ExistsByIdAndUserAsync(PeriodId, UserId, default))
                   .ReturnsAsync(false);

        var act = () => _sut.ExecuteAsync(ValidDto());

        await act.Should().ThrowAsync<DomainException>()
                 .WithMessage("*período*");
        _uow.Verify(u => u.CommitAsync(default), Times.Never);
    }

    [Fact(DisplayName = "Deve lançar exceção se categoria não for acessível ao usuário")]
    public async Task Execute_WithInaccessibleCategory_ShouldThrow()
    {
        _periodRepo.Setup(r => r.ExistsByIdAndUserAsync(PeriodId, UserId, default))
                   .ReturnsAsync(true);
        _categoryRepo.Setup(r => r.IsAccessibleByUserAsync(CategoryId, UserId, default))
                     .ReturnsAsync(false);

        var act = () => _sut.ExecuteAsync(ValidDto());

        await act.Should().ThrowAsync<DomainException>()
                 .WithMessage("*categoria*");
    }

    [Theory(DisplayName = "Deve lançar exceção para Amount inválido")]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Execute_WithInvalidAmount_ShouldThrow(decimal amount)
    {
        SetupValidDependencies();
        var dto = ValidDto() with { Amount = amount };

        var act = () => _sut.ExecuteAsync(dto);
        await act.Should().ThrowAsync<Exception>();
    }
}

public class UpdateExpenseUseCaseTests
{
    private readonly Mock<IExpenseRepository>  _expenseRepo  = new();
    private readonly Mock<ICategoryRepository> _categoryRepo = new();
    private readonly Mock<IUnitOfWork>         _uow          = new();
    private readonly UpdateExpenseUseCase      _sut;

    private static readonly Guid UserId     = Guid.NewGuid();
    private static readonly Guid ExpenseId  = Guid.NewGuid();
    private static readonly Guid CategoryId = Guid.NewGuid();

    public UpdateExpenseUseCaseTests() =>
        _sut = new UpdateExpenseUseCase(_expenseRepo.Object, _categoryRepo.Object, _uow.Object);

    private static Expense FakeExpense() => Expense.Create(
        Guid.NewGuid(), UserId, CategoryId,
        SourceType.Personal, FortnightType.First, PaymentStatus.Pending,
        "Original", 100m, new DateOnly(2026, 4, 10), null, null);

    [Fact(DisplayName = "Deve atualizar despesa existente")]
    public async Task Execute_WithExistingExpense_ShouldUpdate()
    {
        var expense = FakeExpense();
        _expenseRepo.Setup(r => r.GetByIdAndUserAsync(ExpenseId, UserId, default))
                    .ReturnsAsync(expense);
        _categoryRepo.Setup(r => r.IsAccessibleByUserAsync(CategoryId, UserId, default))
                     .ReturnsAsync(true);

        var dto = new UpdateExpenseDto(
            ExpenseId, UserId, CategoryId,
            SourceType.Parental, FortnightType.Second,
            "Internet", 150m, new DateOnly(2026, 4, 20), "Obs", PaymentStatus.Pending);

        await _sut.ExecuteAsync(dto);

        expense.Description.Should().Be("Internet");
        expense.Amount.Should().Be(150m);
        _uow.Verify(u => u.CommitAsync(default), Times.Once);
    }

    [Fact(DisplayName = "Deve lançar exceção se despesa não for do usuário")]
    public async Task Execute_WithUnauthorizedExpense_ShouldThrow()
    {
        _expenseRepo.Setup(r => r.GetByIdAndUserAsync(ExpenseId, UserId, default))
                    .ReturnsAsync((Expense?)null);

        var act = () => _sut.ExecuteAsync(new UpdateExpenseDto(
            ExpenseId, UserId, CategoryId,
            SourceType.Personal, FortnightType.First,
            "Teste", 100m, new DateOnly(2026, 4, 10), null, PaymentStatus.Pending));

        await act.Should().ThrowAsync<DomainException>()
                 .WithMessage("*despesa*");
        _uow.Verify(u => u.CommitAsync(default), Times.Never);
    }
}

public class DeleteExpenseUseCaseTests
{
    private readonly Mock<IExpenseRepository> _expenseRepo = new();
    private readonly Mock<IUnitOfWork>        _uow         = new();
    private readonly DeleteExpenseUseCase     _sut;

    private static readonly Guid UserId    = Guid.NewGuid();
    private static readonly Guid ExpenseId = Guid.NewGuid();

    public DeleteExpenseUseCaseTests() =>
        _sut = new DeleteExpenseUseCase(_expenseRepo.Object, _uow.Object);

    [Fact(DisplayName = "Deve fazer soft delete da despesa")]
    public async Task Execute_WithExistingExpense_ShouldSoftDelete()
    {
        var expense = Expense.Create(
            Guid.NewGuid(), UserId, Guid.NewGuid(),
            SourceType.Personal, FortnightType.First, PaymentStatus.Pending,
            "Aluguel", 1200m, new DateOnly(2026, 4, 10), null, null);

        _expenseRepo.Setup(r => r.GetByIdAndUserAsync(ExpenseId, UserId, default))
                    .ReturnsAsync(expense);

        await _sut.ExecuteAsync(ExpenseId, UserId);

        expense.IsDeleted.Should().BeTrue();
        expense.IsActive.Should().BeFalse();
        _uow.Verify(u => u.CommitAsync(default), Times.Once);
    }

    [Fact(DisplayName = "Deve lançar exceção se despesa não for encontrada")]
    public async Task Execute_WithNotFoundExpense_ShouldThrow()
    {
        _expenseRepo.Setup(r => r.GetByIdAndUserAsync(ExpenseId, UserId, default))
                    .ReturnsAsync((Expense?)null);

        var act = () => _sut.ExecuteAsync(ExpenseId, UserId);

        await act.Should().ThrowAsync<DomainException>();
        _uow.Verify(u => u.CommitAsync(default), Times.Never);
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// INCOME
// ══════════════════════════════════════════════════════════════════════════════

public class CreateIncomeUseCaseTests
{
    private readonly Mock<IIncomeRepository>  _incomeRepo = new();
    private readonly Mock<IPeriodRepository>  _periodRepo = new();
    private readonly Mock<IUnitOfWork>        _uow        = new();
    private readonly CreateIncomeUseCase      _sut;

    private static readonly Guid UserId   = Guid.NewGuid();
    private static readonly Guid PeriodId = Guid.NewGuid();

    public CreateIncomeUseCaseTests() =>
        _sut = new CreateIncomeUseCase(_incomeRepo.Object, _periodRepo.Object, _uow.Object);

    private CreateIncomeDto ValidDto() => new(
        PeriodId:      PeriodId,
        UserId:        UserId,
        FortnightType: FortnightType.First,
        Description:   "Adiantamento MDS",
        Amount:        5500m,
        ReceivedAt:    new DateOnly(2026, 4, 15)
    );

    [Fact(DisplayName = "Deve criar receita com dados válidos")]
    public async Task Execute_WithValidData_ShouldCreateIncome()
    {
        _periodRepo.Setup(r => r.ExistsByIdAndUserAsync(PeriodId, UserId, default))
                   .ReturnsAsync(true);

        var result = await _sut.ExecuteAsync(ValidDto());

        result.Description.Should().Be("Adiantamento MDS");
        result.Amount.Should().Be(5500m);
        _incomeRepo.Verify(r => r.AddAsync(It.IsAny<Income>(), default), Times.Once);
        _uow.Verify(u => u.CommitAsync(default), Times.Once);
    }

    [Fact(DisplayName = "Deve lançar exceção se período não pertencer ao usuário")]
    public async Task Execute_WithUnauthorizedPeriod_ShouldThrow()
    {
        _periodRepo.Setup(r => r.ExistsByIdAndUserAsync(PeriodId, UserId, default))
                   .ReturnsAsync(false);

        var act = () => _sut.ExecuteAsync(ValidDto());

        await act.Should().ThrowAsync<DomainException>()
                 .WithMessage("*período*");
    }

    [Theory(DisplayName = "Deve lançar exceção para Amount inválido")]
    [InlineData(0)]
    [InlineData(-50)]
    public async Task Execute_WithInvalidAmount_ShouldThrow(decimal amount)
    {
        _periodRepo.Setup(r => r.ExistsByIdAndUserAsync(PeriodId, UserId, default))
                   .ReturnsAsync(true);

        var act = () => _sut.ExecuteAsync(ValidDto() with { Amount = amount });
        await act.Should().ThrowAsync<Exception>();
    }
}

public class DeleteIncomeUseCaseTests
{
    private readonly Mock<IIncomeRepository> _incomeRepo = new();
    private readonly Mock<IUnitOfWork>       _uow        = new();
    private readonly DeleteIncomeUseCase     _sut;

    private static readonly Guid UserId   = Guid.NewGuid();
    private static readonly Guid IncomeId = Guid.NewGuid();

    public DeleteIncomeUseCaseTests() =>
        _sut = new DeleteIncomeUseCase(_incomeRepo.Object, _uow.Object);

    [Fact(DisplayName = "Deve fazer soft delete da receita")]
    public async Task Execute_WithExistingIncome_ShouldSoftDelete()
    {
        var income = Income.Create(
            Guid.NewGuid(), UserId, FortnightType.First,
            "Salário", 5500m, new DateOnly(2026, 4, 15), null);

        _incomeRepo.Setup(r => r.GetByIdAndUserAsync(IncomeId, UserId, default))
                   .ReturnsAsync(income);

        await _sut.ExecuteAsync(IncomeId, UserId);

        income.IsDeleted.Should().BeTrue();
        _uow.Verify(u => u.CommitAsync(default), Times.Once);
    }

    [Fact(DisplayName = "Deve lançar exceção se receita não for encontrada")]
    public async Task Execute_WithNotFoundIncome_ShouldThrow()
    {
        _incomeRepo.Setup(r => r.GetByIdAndUserAsync(IncomeId, UserId, default))
                   .ReturnsAsync((Income?)null);

        var act = () => _sut.ExecuteAsync(IncomeId, UserId);

        await act.Should().ThrowAsync<DomainException>();
        _uow.Verify(u => u.CommitAsync(default), Times.Never);
    }
}
