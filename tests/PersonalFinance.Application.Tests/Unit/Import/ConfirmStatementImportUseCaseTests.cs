using FluentAssertions;
using Moq;
using PersonalFinance.Application.DTOs.Import;
using PersonalFinance.Application.UseCases.Import;
using PersonalFinance.Domain.Entities.Financial;
using PersonalFinance.Domain.Enums;
using PersonalFinance.Domain.Exceptions;
using PersonalFinance.Domain.Interfaces.Repositories;
using Xunit;

namespace PersonalFinance.Application.Tests.Unit.Import;

public class ConfirmStatementImportUseCaseTests
{
    private readonly Mock<ICategoryRepository> _catRepo    = new();
    private readonly Mock<IPeriodRepository>   _periodRepo = new();
    private readonly Mock<IExpenseRepository>  _expRepo    = new();
    private readonly Mock<IIncomeRepository>   _incRepo    = new();
    private readonly Mock<IUnitOfWork>         _uow        = new();
    private readonly ConfirmStatementImportUseCase _sut;

    private readonly List<Period>  _addedPeriods  = new();
    private readonly List<Expense> _addedExpenses = new();
    private readonly List<Income>  _addedIncomes  = new();

    private static readonly Guid UserId     = Guid.NewGuid();
    private static readonly Guid CategoryId = Guid.NewGuid();

    public ConfirmStatementImportUseCaseTests()
    {
        _catRepo.Setup(r => r.IsAccessibleByUserAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), default))
                .ReturnsAsync(true);
        _periodRepo.Setup(r => r.GetByUserYearMonthAsync(
                It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>(), default))
                .ReturnsAsync((Period?)null);
        _periodRepo.Setup(r => r.AddAsync(It.IsAny<Period>(), default))
                .Callback<Period, CancellationToken>((p, _) => _addedPeriods.Add(p))
                .Returns(Task.CompletedTask);
        _expRepo.Setup(r => r.AddAsync(It.IsAny<Expense>(), default))
                .Callback<Expense, CancellationToken>((e, _) => _addedExpenses.Add(e))
                .Returns(Task.CompletedTask);
        _incRepo.Setup(r => r.AddAsync(It.IsAny<Income>(), default))
                .Callback<Income, CancellationToken>((i, _) => _addedIncomes.Add(i))
                .Returns(Task.CompletedTask);

        _sut = new ConfirmStatementImportUseCase(
            _catRepo.Object, _periodRepo.Object, _expRepo.Object, _incRepo.Object, _uow.Object);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static DateOnly PastDate() => DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));

    private static ConfirmStatementItemDto Exp(
        DateOnly date, decimal amount = 50m, string desc = "MERCADO",
        Guid? categoryId = null, string? sourceType = null, bool noCategory = false) =>
        new(date, desc, amount, "Expense", noCategory ? null : categoryId ?? CategoryId, sourceType);

    private static ConfirmStatementItemDto Inc(DateOnly date, decimal amount = 100m, string desc = "SALARIO") =>
        new(date, desc, amount, "Income", null, null);

    private Task<ConfirmStatementImportResultDto> Run(params ConfirmStatementItemDto[] items) =>
        _sut.ExecuteAsync(new ConfirmStatementImportRequestDto(items), UserId);

    private Period SetupExistingPeriod(int year, int month, bool softDeleted = false)
    {
        var period = Period.Create(UserId, year, month);
        if (softDeleted) period.SoftDelete();
        _periodRepo.Setup(r => r.GetByUserYearMonthAsync(UserId, year, month, default))
                   .ReturnsAsync(period);
        return period;
    }

    // ── Multi-período ─────────────────────────────────────────────────────────

    [Fact(DisplayName = "Itens em dois meses criam dois Periods com registros no PeriodId correto")]
    public async Task Confirm_TwoMonths_CreatesTwoPeriodsWithCorrectAssociation()
    {
        var result = await Run(
            Exp(new DateOnly(2025, 1, 20), 10m, "A"),
            Inc(new DateOnly(2025, 1, 21), 20m, "B"),
            Exp(new DateOnly(2025, 2, 3), 30m, "C"),
            Inc(new DateOnly(2025, 2, 4), 40m, "D"));

        result.PeriodsCreated.Should().Be(2);
        result.PeriodsReused.Should().Be(0);
        result.ExpensesCreated.Should().Be(2);
        result.IncomesCreated.Should().Be(2);

        var jan = _addedPeriods.Single(p => p.Month == 1 && p.Year == 2025);
        var feb = _addedPeriods.Single(p => p.Month == 2 && p.Year == 2025);
        _addedExpenses.Single(e => e.Description == "A").PeriodId.Should().Be(jan.Id);
        _addedIncomes.Single(i => i.Description == "B").PeriodId.Should().Be(jan.Id);
        _addedExpenses.Single(e => e.Description == "C").PeriodId.Should().Be(feb.Id);
        _addedIncomes.Single(i => i.Description == "D").PeriodId.Should().Be(feb.Id);
        _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "Vários itens do mesmo mês novo criam apenas um Period")]
    public async Task Confirm_SameNewMonth_CreatesSinglePeriod()
    {
        var result = await Run(
            Exp(new DateOnly(2025, 3, 1)),
            Exp(new DateOnly(2025, 3, 10)),
            Inc(new DateOnly(2025, 3, 20)));

        result.PeriodsCreated.Should().Be(1);
        _addedPeriods.Should().HaveCount(1);
        _periodRepo.Verify(r => r.AddAsync(It.IsAny<Period>(), default), Times.Once);
        _addedExpenses.Select(e => e.PeriodId).Concat(_addedIncomes.Select(i => i.PeriodId))
            .Distinct().Should().ContainSingle().Which.Should().Be(_addedPeriods[0].Id);
    }

    [Fact(DisplayName = "Period existente é reaproveitado sem AddAsync")]
    public async Task Confirm_ExistingPeriod_IsReused()
    {
        var existing = SetupExistingPeriod(2025, 4);

        var result = await Run(Exp(new DateOnly(2025, 4, 5)), Inc(new DateOnly(2025, 4, 6)));

        result.PeriodsCreated.Should().Be(0);
        result.PeriodsReused.Should().Be(1);
        _periodRepo.Verify(r => r.AddAsync(It.IsAny<Period>(), default), Times.Never);
        _addedExpenses.Single().PeriodId.Should().Be(existing.Id);
        _addedIncomes.Single().PeriodId.Should().Be(existing.Id);
    }

    [Fact(DisplayName = "Period soft-deleted é reativado e recebe os registros")]
    public async Task Confirm_SoftDeletedPeriod_IsReactivated()
    {
        var existing = SetupExistingPeriod(2025, 5, softDeleted: true);

        var result = await Run(Exp(new DateOnly(2025, 5, 5)));

        existing.IsDeleted.Should().BeFalse();
        _periodRepo.Verify(r => r.UpdateAsync(existing, default), Times.Once);
        _periodRepo.Verify(r => r.AddAsync(It.IsAny<Period>(), default), Times.Never);
        result.PeriodsReused.Should().Be(1);
        _addedExpenses.Single().PeriodId.Should().Be(existing.Id);
    }

    // ── Mapeamento ────────────────────────────────────────────────────────────

    [Fact(DisplayName = "Expense é gravada como Paid com PaymentDate e DueDate iguais à data do lançamento")]
    public async Task Confirm_Expense_IsPaidWithPaymentDateEqualToDate()
    {
        var date = PastDate();

        await Run(Exp(date, 42.5m, "PADARIA"));

        var e = _addedExpenses.Single();
        e.PaymentStatus.Should().Be(PaymentStatus.Paid);
        e.PaymentDate.Should().Be(date);
        e.DueDate.Should().Be(date);
        e.Amount.Should().Be(42.5m);
        e.Description.Should().Be("PADARIA");
        e.CategoryId.Should().Be(CategoryId);
        e.UserId.Should().Be(UserId);
    }

    [Fact(DisplayName = "Income tem ReceivedAt igual à data do lançamento")]
    public async Task Confirm_Income_HasReceivedAtEqualToDate()
    {
        var date = PastDate();

        await Run(Inc(date, 999m, "SALARIO"));

        var i = _addedIncomes.Single();
        i.ReceivedAt.Should().Be(date);
        i.Amount.Should().Be(999m);
        i.UserId.Should().Be(UserId);
    }

    [Fact(DisplayName = "Income ignora CategoryId e não valida acessibilidade")]
    public async Task Confirm_Income_IgnoresCategory()
    {
        var item = new ConfirmStatementItemDto(PastDate(), "SALARIO", 100m, "Income", Guid.NewGuid(), null);

        await Run(item);

        _catRepo.Verify(r => r.IsAccessibleByUserAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), default), Times.Never);
        _addedIncomes.Should().HaveCount(1);
    }

    [Theory(DisplayName = "Quinzena derivada do dia: <=15 First, >15 Second")]
    [InlineData(1, FortnightType.First)]
    [InlineData(15, FortnightType.First)]
    [InlineData(16, FortnightType.Second)]
    [InlineData(28, FortnightType.Second)]
    public async Task Confirm_Fortnight_DerivedFromDay(int day, FortnightType expected)
    {
        var date = new DateOnly(2025, 6, day);

        await Run(Exp(date), Inc(date));

        _addedExpenses.Single().FortnightType.Should().Be(expected);
        _addedIncomes.Single().FortnightType.Should().Be(expected);
    }

    [Fact(DisplayName = "SourceType padrão é Personal quando não informado")]
    public async Task Confirm_SourceType_DefaultsToPersonal()
    {
        await Run(Exp(PastDate()));

        _addedExpenses.Single().SourceType.Should().Be(SourceType.Personal);
    }

    [Fact(DisplayName = "SourceType informado é respeitado")]
    public async Task Confirm_SourceType_Provided_IsRespected()
    {
        await Run(Exp(PastDate(), sourceType: "Parental"));

        _addedExpenses.Single().SourceType.Should().Be(SourceType.Parental);
    }

    // ── Validações (atomicidade: nada é gravado) ──────────────────────────────

    private async Task AssertRejectedAsync(params ConfirmStatementItemDto[] items)
    {
        var act = () => Run(items);

        await act.Should().ThrowAsync<DomainException>();
        _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        _addedExpenses.Should().BeEmpty();
        _addedIncomes.Should().BeEmpty();
    }

    [Fact(DisplayName = "Data futura rejeita a importação inteira e nunca commita")]
    public async Task Confirm_FutureDate_ThrowsAndNeverCommits()
    {
        var future = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5));

        await AssertRejectedAsync(Exp(PastDate()), Exp(future));
    }

    [Fact(DisplayName = "Expense sem categoria rejeita a importação inteira")]
    public async Task Confirm_ExpenseWithoutCategory_Throws()
    {
        await AssertRejectedAsync(Inc(PastDate()), Exp(PastDate(), noCategory: true));
    }

    [Fact(DisplayName = "Expense com categoria inacessível rejeita a importação inteira")]
    public async Task Confirm_InaccessibleCategory_Throws()
    {
        var foreign = Guid.NewGuid();
        _catRepo.Setup(r => r.IsAccessibleByUserAsync(foreign, UserId, default)).ReturnsAsync(false);

        await AssertRejectedAsync(Exp(PastDate(), categoryId: foreign));
    }

    [Fact(DisplayName = "Kind inválido é rejeitado")]
    public async Task Confirm_InvalidKind_Throws()
    {
        await AssertRejectedAsync(new ConfirmStatementItemDto(PastDate(), "X", 10m, "Transfer", CategoryId, null));
    }

    [Theory(DisplayName = "Amount menor ou igual a zero é rejeitado")]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task Confirm_NonPositiveAmount_Throws(int amount)
    {
        await AssertRejectedAsync(Exp(PastDate(), amount));
    }

    [Fact(DisplayName = "Lista vazia é rejeitada")]
    public async Task Confirm_EmptyList_Throws()
    {
        await AssertRejectedAsync();
    }

    [Theory(DisplayName = "Description vazia é rejeitada")]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Confirm_EmptyDescription_Throws(string desc)
    {
        await AssertRejectedAsync(Exp(PastDate(), desc: desc));
    }

    [Fact(DisplayName = "Description com mais de 200 caracteres é rejeitada (Expense e Income)")]
    public async Task Confirm_DescriptionOver200_Throws()
    {
        var tooLong = new string('a', 201);

        await AssertRejectedAsync(Exp(PastDate(), desc: tooLong));
        await AssertRejectedAsync(Inc(PastDate(), desc: tooLong));
    }

    [Fact(DisplayName = "Description com exatamente 200 caracteres é aceita")]
    public async Task Confirm_Description200_IsAccepted()
    {
        await Run(Exp(PastDate(), desc: new string('a', 200)));

        _addedExpenses.Should().HaveCount(1);
    }

    [Fact(DisplayName = "Data de hoje (UTC) é aceita")]
    public async Task Confirm_TodayDate_IsAccepted()
    {
        await Run(Exp(DateOnly.FromDateTime(DateTime.UtcNow)));

        _addedExpenses.Should().HaveCount(1);
    }
}
