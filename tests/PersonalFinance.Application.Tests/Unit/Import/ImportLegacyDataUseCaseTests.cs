using FluentAssertions;
using Moq;
using PersonalFinance.Application.DTOs.Import;
using PersonalFinance.Application.Interfaces;
using PersonalFinance.Application.UseCases.Import;
using PersonalFinance.Domain.Entities.Config;
using PersonalFinance.Domain.Entities.Financial;
using PersonalFinance.Domain.Enums;
using PersonalFinance.Domain.Interfaces.Repositories;
using Xunit;

namespace PersonalFinance.Application.Tests.Unit.Import;

public class ImportLegacyDataUseCaseTests
{
    private readonly Mock<IExcelParserService>  _parser     = new();
    private readonly Mock<ICategoryRepository>  _catRepo    = new();
    private readonly Mock<IPeriodRepository>    _periodRepo = new();
    private readonly Mock<IExpenseRepository>   _expRepo    = new();
    private readonly Mock<IIncomeRepository>    _incRepo    = new();
    private readonly Mock<IUnitOfWork>          _uow        = new();
    private readonly ImportLegacyDataUseCase    _sut;

    private static readonly Guid UserId   = Guid.NewGuid();
    private static readonly Guid PeriodId = Guid.NewGuid();

    public ImportLegacyDataUseCaseTests()
    {
        _sut = new ImportLegacyDataUseCase(
            _parser.Object, _catRepo.Object, _periodRepo.Object,
            _expRepo.Object, _incRepo.Object, _uow.Object);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static ParsedSheetDto BuildSheet(
        int year = 2026, int month = 4,
        IEnumerable<ParsedExpenseDto>? expenses = null,
        IEnumerable<ParsedIncomeDto>?  incomes  = null) =>
        new(year, month,
            (incomes  ?? Enumerable.Empty<ParsedIncomeDto>()).ToList(),
            (expenses ?? Enumerable.Empty<ParsedExpenseDto>()).ToList(),
            new List<string>());

    private static ParsedExpenseDto BuildExpenseDto(
        string description = "Internet", decimal amount = 110m,
        bool isPaid = false, FortnightType fortnight = FortnightType.First)
    {
        // Usa sempre uma data no passado para evitar falha na validação de MarkAsPaid
        var pastDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
        return new(description, amount, SourceType.Personal, fortnight, isPaid,
            pastDate, "Gasto Recorrente");
    }

    private static ParsedIncomeDto BuildIncomeDto(
        string description = "Receita 1ª Quinzena", decimal amount = 2613m,
        FortnightType fortnight = FortnightType.First) =>
        new(description, amount, fortnight, new DateOnly(2026, 4, 1));

    private void SetupNoPeriod() =>
        _periodRepo.Setup(r => r.GetByUserYearMonthAsync(UserId, It.IsAny<int>(), It.IsAny<int>(), default))
                   .ReturnsAsync((Period?)null);

    private Period SetupExistingPeriod()
    {
        var period = Period.Create(UserId, 2026, 4);
        _periodRepo.Setup(r => r.GetByUserYearMonthAsync(UserId, 2026, 4, default))
                   .ReturnsAsync(period);
        return period;
    }

    private void SetupCategoryNotFound() =>
        _catRepo.Setup(r => r.GetGlobalByNameAsync(It.IsAny<string>(), default))
                .ReturnsAsync((Category?)null);

    private void SetupAllCategoriesExist()
    {
        var cat = Category.CreateGlobal("Gasto Recorrente", "#527a58", null);
        var rec = Category.CreateGlobal("Receita Diversa",  "#6b8f71", null);
        _catRepo.Setup(r => r.GetGlobalByNameAsync("Gasto Recorrente", default)).ReturnsAsync(cat);
        _catRepo.Setup(r => r.GetGlobalByNameAsync("Receita Diversa",  default)).ReturnsAsync(rec);
    }

    // ── Criação — período novo ────────────────────────────────────────────────

    [Fact(DisplayName = "Deve criar período e inserir registros quando não existem")]
    public async Task Execute_NewPeriod_ShouldCreateAndInsert()
    {
        _parser.Setup(p => p.ParseAsync(It.IsAny<Stream>(), default))
               .ReturnsAsync(new[] { BuildSheet(expenses: new[] { BuildExpenseDto() }) });

        SetupNoPeriod();
        SetupCategoryNotFound();
        _expRepo.Setup(r => r.FindByImportKeyAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<decimal>(),
            It.IsAny<FortnightType>(), default))
            .ReturnsAsync((Expense?)null);

        var result = await _sut.ExecuteAsync(Stream.Null, UserId);

        result.PeriodsCreated.Should().Be(1);
        result.PeriodsSkipped.Should().Be(0);
        result.ExpensesImported.Should().Be(1);
        _expRepo.Verify(r => r.AddAsync(It.IsAny<Expense>(), default), Times.Once);
    }

    // ── Reutilização de período existente ─────────────────────────────────────

    [Fact(DisplayName = "Deve reutilizar período existente sem criar duplicata")]
    public async Task Execute_ExistingPeriod_ShouldReuseNotDuplicate()
    {
        _parser.Setup(p => p.ParseAsync(It.IsAny<Stream>(), default))
               .ReturnsAsync(new[] { BuildSheet() });

        SetupExistingPeriod();
        SetupAllCategoriesExist();

        var result = await _sut.ExecuteAsync(Stream.Null, UserId);

        result.PeriodsSkipped.Should().Be(1);
        result.PeriodsCreated.Should().Be(0);
        _periodRepo.Verify(r => r.AddAsync(It.IsAny<Period>(), default), Times.Never);
    }

    // ── Insert de despesa nova ────────────────────────────────────────────────

    [Fact(DisplayName = "Deve inserir despesa quando não existe no banco")]
    public async Task Execute_NewExpense_ShouldInsert()
    {
        _parser.Setup(p => p.ParseAsync(It.IsAny<Stream>(), default))
               .ReturnsAsync(new[] { BuildSheet(expenses: new[] { BuildExpenseDto() }) });

        SetupExistingPeriod();
        SetupAllCategoriesExist();
        _expRepo.Setup(r => r.FindByImportKeyAsync(
            It.IsAny<Guid>(), "Internet", 110m, FortnightType.First, default))
            .ReturnsAsync((Expense?)null);

        var result = await _sut.ExecuteAsync(Stream.Null, UserId);

        result.ExpensesImported.Should().Be(1);
        _expRepo.Verify(r => r.AddAsync(It.IsAny<Expense>(), default), Times.Once);
        _expRepo.Verify(r => r.UpdateAsync(It.IsAny<Expense>(), default), Times.Never);
    }

    // ── Update de despesa — status mudou ─────────────────────────────────────

    [Fact(DisplayName = "Deve atualizar despesa quando status mudou de Pending para Paid")]
    public async Task Execute_ExpenseStatusChanged_ShouldUpdate()
    {
        var dto = BuildExpenseDto(isPaid: true); // planilha diz SIM

        // Banco tem a mesma despesa como Pending — usa mesma data do dto para não acionar diff de DueDate
        var existing = Expense.Create(
            PeriodId, UserId, Guid.NewGuid(),
            SourceType.Personal, FortnightType.First,
            PaymentStatus.Pending,                    // status diferente
            "Internet", 110m,
            dto.DueDate, null, null);

        _parser.Setup(p => p.ParseAsync(It.IsAny<Stream>(), default))
               .ReturnsAsync(new[] { BuildSheet(expenses: new[] { dto }) });

        SetupExistingPeriod();
        SetupAllCategoriesExist();
        _expRepo.Setup(r => r.FindByImportKeyAsync(
            It.IsAny<Guid>(), "Internet", 110m, FortnightType.First, default))
            .ReturnsAsync(existing);

        var result = await _sut.ExecuteAsync(Stream.Null, UserId);

        result.ExpensesImported.Should().Be(1);
        _expRepo.Verify(r => r.UpdateAsync(existing, default), Times.Once);
        _expRepo.Verify(r => r.AddAsync(It.IsAny<Expense>(), default), Times.Never);
    }

    // ── Sem mudança — ignora update desnecessário ─────────────────────────────

    [Fact(DisplayName = "Deve ignorar despesa quando dados são idênticos")]
    public async Task Execute_ExpenseUnchanged_ShouldSkipUpdate()
    {
        var dto = BuildExpenseDto(isPaid: false);

        var existing = Expense.Create(
            PeriodId, UserId, Guid.NewGuid(),
            SourceType.Personal, FortnightType.First,
            PaymentStatus.Pending,                    // igual ao dto
            "Internet", 110m,
            new DateOnly(2026, 4, 20), null, null);

        _parser.Setup(p => p.ParseAsync(It.IsAny<Stream>(), default))
               .ReturnsAsync(new[] { BuildSheet(expenses: new[] { dto }) });

        SetupExistingPeriod();
        SetupAllCategoriesExist();
        _expRepo.Setup(r => r.FindByImportKeyAsync(
            It.IsAny<Guid>(), "Internet", 110m, FortnightType.First, default))
            .ReturnsAsync(existing);

        var result = await _sut.ExecuteAsync(Stream.Null, UserId);

        result.ExpensesImported.Should().Be(0);
        _expRepo.Verify(r => r.AddAsync(It.IsAny<Expense>(), default),    Times.Never);
        _expRepo.Verify(r => r.UpdateAsync(It.IsAny<Expense>(), default), Times.Never);
    }

    // ── Insert de receita nova ────────────────────────────────────────────────

    [Fact(DisplayName = "Deve inserir receita quando não existe no banco")]
    public async Task Execute_NewIncome_ShouldInsert()
    {
        _parser.Setup(p => p.ParseAsync(It.IsAny<Stream>(), default))
               .ReturnsAsync(new[] { BuildSheet(incomes: new[] { BuildIncomeDto() }) });

        SetupExistingPeriod();
        SetupAllCategoriesExist();
        _incRepo.Setup(r => r.FindByImportKeyAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<decimal>(),
            It.IsAny<FortnightType>(), default))
            .ReturnsAsync((Income?)null);

        var result = await _sut.ExecuteAsync(Stream.Null, UserId);

        result.IncomesImported.Should().Be(1);
        _incRepo.Verify(r => r.AddAsync(It.IsAny<Income>(), default), Times.Once);
    }

    // ── Update de receita ─────────────────────────────────────────────────────

    [Fact(DisplayName = "Deve atualizar receita quando data de recebimento mudou")]
    public async Task Execute_IncomeReceivedAtChanged_ShouldUpdate()
    {
        var dto = BuildIncomeDto();

        var existing = Income.Create(
            PeriodId, UserId, FortnightType.First,
            "Receita 1ª Quinzena", 2613m,
            new DateOnly(2026, 4, 5),  // data diferente
            null);

        _parser.Setup(p => p.ParseAsync(It.IsAny<Stream>(), default))
               .ReturnsAsync(new[] { BuildSheet(incomes: new[] { dto }) });

        SetupExistingPeriod();
        SetupAllCategoriesExist();
        _incRepo.Setup(r => r.FindByImportKeyAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<decimal>(),
            It.IsAny<FortnightType>(), default))
            .ReturnsAsync(existing);

        var result = await _sut.ExecuteAsync(Stream.Null, UserId);

        result.IncomesImported.Should().Be(1);
        _incRepo.Verify(r => r.UpdateAsync(existing, default), Times.Once);
        _incRepo.Verify(r => r.AddAsync(It.IsAny<Income>(), default), Times.Never);
    }

    // ── Valor zero → skip ─────────────────────────────────────────────────────

    [Fact(DisplayName = "Deve ignorar despesas e receitas com valor zero")]
    public async Task Execute_ZeroAmount_ShouldSkip()
    {
        _parser.Setup(p => p.ParseAsync(It.IsAny<Stream>(), default))
               .ReturnsAsync(new[]
               {
                   BuildSheet(
                       expenses: new[] { BuildExpenseDto(amount: 0m) },
                       incomes:  new[] { BuildIncomeDto(amount: 0m) })
               });

        SetupExistingPeriod();
        SetupAllCategoriesExist();

        var result = await _sut.ExecuteAsync(Stream.Null, UserId);

        result.ExpensesSkipped.Should().Be(1);
        result.IncomesSkipped.Should().Be(1);
        _expRepo.Verify(r => r.AddAsync(It.IsAny<Expense>(), default), Times.Never);
        _incRepo.Verify(r => r.AddAsync(It.IsAny<Income>(), default),  Times.Never);
    }
}
