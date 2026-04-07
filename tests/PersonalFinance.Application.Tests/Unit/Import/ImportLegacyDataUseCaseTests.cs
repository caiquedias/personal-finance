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
    private readonly Mock<IExcelParserService>   _parser     = new();
    private readonly Mock<ICategoryRepository>   _catRepo    = new();
    private readonly Mock<IPeriodRepository>     _periodRepo = new();
    private readonly Mock<IExpenseRepository>    _expRepo    = new();
    private readonly Mock<IIncomeRepository>     _incRepo    = new();
    private readonly Mock<IUnitOfWork>           _uow        = new();
    private readonly ImportLegacyDataUseCase     _sut;

    private static readonly Guid UserId = Guid.NewGuid();

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
        IEnumerable<ParsedIncomeDto>? incomes = null) =>
        new(year, month,
            (incomes ?? Enumerable.Empty<ParsedIncomeDto>()).ToList(),
            (expenses ?? Enumerable.Empty<ParsedExpenseDto>()).ToList(),
            new List<string>());

    private void SetupNewPeriod() =>
        _periodRepo.Setup(r => r.ExistsAsync(
            UserId, It.IsAny<int>(), It.IsAny<int>(), default))
            .ReturnsAsync(false);

    private void SetupCategoryNotFound() =>
        _catRepo.Setup(r => r.GetGlobalByNameAsync(
            It.IsAny<string>(), default))
            .ReturnsAsync((Category?)null);

    // ── Testes de fluxo principal ─────────────────────────────────────────────

    [Fact(DisplayName = "Deve criar período, despesas e receitas para aba válida")]
    public async Task Execute_WithValidSheet_ShouldCreateAllEntities()
    {
        _parser.Setup(p => p.ParseAsync(It.IsAny<Stream>(), default))
               .ReturnsAsync(new List<ParsedSheetDto>
               {
                   BuildSheet(
                       expenses: new[]
                       {
                           new ParsedExpenseDto("Internet", 110.99m, SourceType.Personal,
                               FortnightType.First, true,
                               new DateOnly(2026,4,20), "Gasto Recorrente")
                       },
                       incomes: new[]
                       {
                           new ParsedIncomeDto("Receita 1ª Quinzena", 2613.15m,
                               FortnightType.First, new DateOnly(2026,4,1))
                       })
               });

        SetupNewPeriod();
        SetupCategoryNotFound();

        var result = await _sut.ExecuteAsync(Stream.Null, UserId);

        result.PeriodsCreated.Should().Be(1);
        result.ExpensesImported.Should().Be(1);
        result.IncomesImported.Should().Be(1);
        _uow.Verify(u => u.CommitAsync(default), Times.AtLeast(2));
    }

    [Fact(DisplayName = "Deve ignorar período já existente e não reimportar")]
    public async Task Execute_ExistingPeriod_ShouldSkipAndNotImport()
    {
        _parser.Setup(p => p.ParseAsync(It.IsAny<Stream>(), default))
               .ReturnsAsync(new List<ParsedSheetDto>
               {
                   BuildSheet(
                       expenses: new[]
                       {
                           new ParsedExpenseDto("Internet", 110.99m, SourceType.Personal,
                               FortnightType.First, true,
                               new DateOnly(2026,4,20), "Gasto Recorrente")
                       })
               });

        // Período JÁ existe
        _periodRepo.Setup(r => r.ExistsAsync(UserId, 2026, 4, default))
                   .ReturnsAsync(true);

        SetupCategoryNotFound();

        var result = await _sut.ExecuteAsync(Stream.Null, UserId);

        result.PeriodsSkipped.Should().Be(1);
        result.PeriodsCreated.Should().Be(0);
        result.ExpensesImported.Should().Be(0);
        _expRepo.Verify(r => r.AddAsync(It.IsAny<Expense>(), default), Times.Never);
    }

    [Fact(DisplayName = "Deve criar categoria global quando não existe")]
    public async Task Execute_NewCategory_ShouldCreateGlobalCategory()
    {
        _parser.Setup(p => p.ParseAsync(It.IsAny<Stream>(), default))
               .ReturnsAsync(new List<ParsedSheetDto>
               {
                   BuildSheet(expenses: new[]
                   {
                       new ParsedExpenseDto("Uber", 29.90m, SourceType.Personal,
                           FortnightType.First, true,
                           new DateOnly(2026,4,1), "Uber")
                   })
               });

        SetupNewPeriod();
        _catRepo.Setup(r => r.GetGlobalByNameAsync("Uber", default))
                .ReturnsAsync((Category?)null);

        var result = await _sut.ExecuteAsync(Stream.Null, UserId);

        result.CategoriesCreated.Should().BeGreaterThan(0);
        _catRepo.Verify(r => r.AddAsync(
            It.Is<Category>(c => c.Name == "Uber" && c.IsGlobal), default),
            Times.Once);
    }

    [Fact(DisplayName = "Deve reutilizar categoria global existente sem criar duplicata")]
    public async Task Execute_ExistingCategory_ShouldReuseAndNotCreateDuplicate()
    {
        var existing = Category.CreateGlobal("Gasto Recorrente", "#527a58", null);

        _parser.Setup(p => p.ParseAsync(It.IsAny<Stream>(), default))
               .ReturnsAsync(new List<ParsedSheetDto>
               {
                   BuildSheet(expenses: new[]
                   {
                       new ParsedExpenseDto("Internet", 110m, SourceType.Personal,
                           FortnightType.First, false,
                           new DateOnly(2026,4,20), "Gasto Recorrente")
                   })
               });

        SetupNewPeriod();

        // Mocka "Gasto Recorrente" como já existente
        _catRepo.Setup(r => r.GetGlobalByNameAsync("Gasto Recorrente", default))
                .ReturnsAsync(existing);

        // "Receita Diversa" também é criada pelo use case — mocka como já existente
        // para garantir que CategoriesCreated permaneça 0
        var receitaDiversa = Category.CreateGlobal("Receita Diversa", "#6b8f71", null);
        _catRepo.Setup(r => r.GetGlobalByNameAsync("Receita Diversa", default))
                .ReturnsAsync(receitaDiversa);

        var result = await _sut.ExecuteAsync(Stream.Null, UserId);

        // Não deve criar categoria nova — ambas já existem
        _catRepo.Verify(r => r.AddAsync(
            It.Is<Category>(c => c.Name == "Gasto Recorrente"), default), Times.Never);
        result.CategoriesCreated.Should().Be(0);
    }

    [Fact(DisplayName = "Deve ignorar despesas com valor zero")]
    public async Task Execute_ZeroAmountExpense_ShouldSkip()
    {
        _parser.Setup(p => p.ParseAsync(It.IsAny<Stream>(), default))
               .ReturnsAsync(new List<ParsedSheetDto>
               {
                   BuildSheet(expenses: new[]
                   {
                       new ParsedExpenseDto("Zero", 0m, SourceType.Personal,
                           FortnightType.First, false,
                           new DateOnly(2026,4,1), "Gasto Recorrente")
                   })
               });

        SetupNewPeriod();
        SetupCategoryNotFound();

        var result = await _sut.ExecuteAsync(Stream.Null, UserId);

        result.ExpensesSkipped.Should().Be(1);
        result.ExpensesImported.Should().Be(0);
        _expRepo.Verify(r => r.AddAsync(It.IsAny<Expense>(), default), Times.Never);
    }

    [Fact(DisplayName = "Deve processar múltiplas abas corretamente")]
    public async Task Execute_MultipleSheets_ShouldProcessAll()
    {
        _parser.Setup(p => p.ParseAsync(It.IsAny<Stream>(), default))
               .ReturnsAsync(new List<ParsedSheetDto>
               {
                   BuildSheet(2026, 3, expenses: new[]
                   {
                       new ParsedExpenseDto("Gás", 44.47m, SourceType.Personal,
                           FortnightType.First, true, new DateOnly(2026,3,5), "Gasto Recorrente")
                   }),
                   BuildSheet(2026, 4, expenses: new[]
                   {
                       new ParsedExpenseDto("Gás", 50m, SourceType.Personal,
                           FortnightType.First, false, new DateOnly(2026,4,5), "Gasto Recorrente")
                   }),
               });

        _periodRepo.Setup(r => r.ExistsAsync(UserId, 2026, 3, default)).ReturnsAsync(false);
        _periodRepo.Setup(r => r.ExistsAsync(UserId, 2026, 4, default)).ReturnsAsync(false);
        SetupCategoryNotFound();

        var result = await _sut.ExecuteAsync(Stream.Null, UserId);

        result.PeriodsCreated.Should().Be(2);
        result.ExpensesImported.Should().Be(2);
    }

    [Fact(DisplayName = "Deve marcar despesa paga com PaymentDate = DueDate")]
    public async Task Execute_PaidExpense_ShouldHavePaymentDate()
    {
        var dueDate = new DateOnly(2026, 4, 10);
        Expense? capturedExpense = null;

        _parser.Setup(p => p.ParseAsync(It.IsAny<Stream>(), default))
               .ReturnsAsync(new List<ParsedSheetDto>
               {
                   BuildSheet(expenses: new[]
                   {
                       new ParsedExpenseDto("Condomínio", 600m, SourceType.Parental,
                           FortnightType.First, true, dueDate, "Gasto Recorrente")
                   })
               });

        SetupNewPeriod();
        SetupCategoryNotFound();

        _expRepo.Setup(r => r.AddAsync(It.IsAny<Expense>(), default))
                .Callback<Expense, CancellationToken>((e, _) => capturedExpense = e);

        await _sut.ExecuteAsync(Stream.Null, UserId);

        capturedExpense.Should().NotBeNull();
        capturedExpense!.PaymentStatus.Should().Be(PaymentStatus.Paid);
        capturedExpense.PaymentDate.Should().Be(dueDate);
    }

    [Fact(DisplayName = "Resultado deve incluir avisos das abas parseadas")]
    public async Task Execute_SheetWithWarnings_ShouldIncludeInResult()
    {
        var sheetWithWarning = new ParsedSheetDto(
            2026, 4,
            new List<ParsedIncomeDto>(),
            new List<ParsedExpenseDto>(),
            new List<string> { "Aviso de teste" }
        );

        _parser.Setup(p => p.ParseAsync(It.IsAny<Stream>(), default))
               .ReturnsAsync(new List<ParsedSheetDto> { sheetWithWarning });

        SetupNewPeriod();
        SetupCategoryNotFound();

        var result = await _sut.ExecuteAsync(Stream.Null, UserId);

        result.Warnings.Should().Contain("Aviso de teste");
    }
}
