using FluentAssertions;
using PersonalFinance.Domain.Entities.Financial;
using PersonalFinance.Domain.Exceptions;
using Xunit;

namespace PersonalFinance.Domain.Tests.Unit.Financial;

/// <summary>
/// Testes da entidade PurgeRecord (registro de expurgo de período).
/// Regras: todos os campos obrigatórios; PurgedAt deve ser UTC; UserId não pode ser vazio.
/// </summary>
public class PurgeRecordTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    private static PurgeRecord CreateValid() => PurgeRecord.Create(
        userId:              UserId,
        periodYear:          (short)2026,
        periodMonth:         (byte)4,
        totalIncome:         5000m,
        totalExpense:        3000m,
        incomeCount:         2,
        expenseCount:        10,
        categorySummaryJson: "{\"Alimentação\":500}",
        csvFileName:         "expurgo_2026_04.csv"
    );

    // ── Criação válida ────────────────────────────────────────────────────────

    [Fact(DisplayName = "Deve criar PurgeRecord com dados válidos")]
    public void Create_WithValidData_ShouldSucceed()
    {
        var record = CreateValid();

        record.UserId.Should().Be(UserId);
        record.PeriodYear.Should().Be(2026);
        record.PeriodMonth.Should().Be(4);
        record.TotalIncome.Should().Be(5000m);
        record.TotalExpense.Should().Be(3000m);
        record.IncomeCount.Should().Be(2);
        record.ExpenseCount.Should().Be(10);
        record.CategorySummaryJson.Should().Be("{\"Alimentação\":500}");
        record.CsvFileName.Should().Be("expurgo_2026_04.csv");
        record.IsActive.Should().BeTrue();
    }

    [Fact(DisplayName = "PurgedAt deve ser UTC")]
    public void Create_PurgedAt_ShouldBeUtc()
    {
        var record = CreateValid();
        record.PurgedAt.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact(DisplayName = "PurgedAt deve ser próximo do momento atual")]
    public void Create_PurgedAt_ShouldBeNearNow()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);
        var record = CreateValid();
        var after  = DateTime.UtcNow.AddSeconds(1);

        record.PurgedAt.Should().BeAfter(before).And.BeBefore(after);
    }

    [Fact(DisplayName = "Id deve ser gerado automaticamente (Guid não vazio)")]
    public void Create_Id_ShouldBeGenerated()
    {
        var record = CreateValid();
        record.Id.Should().NotBeEmpty();
    }

    // ── Validação de UserId ───────────────────────────────────────────────────

    [Fact(DisplayName = "Deve lançar exceção para UserId vazio")]
    public void Create_WithEmptyUserId_ShouldThrow()
    {
        var act = () => PurgeRecord.Create(
            userId:              Guid.Empty,
            periodYear:          2026,
            periodMonth:         4,
            totalIncome:         5000m,
            totalExpense:        3000m,
            incomeCount:         2,
            expenseCount:        10,
            categorySummaryJson: "{}",
            csvFileName:         "file.csv"
        );

        act.Should().Throw<DomainException>();
    }

    // ── Validação de mês ──────────────────────────────────────────────────────

    [Theory(DisplayName = "Deve lançar exceção para mês inválido")]
    [InlineData(0)]
    [InlineData(13)]
    [InlineData(-1)]
    public void Create_WithInvalidMonth_ShouldThrow(int month)
    {
        var act = () => PurgeRecord.Create(
            userId:              UserId,
            periodYear:          2026,
            periodMonth:         month,
            totalIncome:         5000m,
            totalExpense:        3000m,
            incomeCount:         2,
            expenseCount:        10,
            categorySummaryJson: "{}",
            csvFileName:         "file.csv"
        );

        act.Should().Throw<DomainException>();
    }

    [Theory(DisplayName = "Deve aceitar todos os meses válidos (1–12)")]
    [InlineData(1)]
    [InlineData(6)]
    [InlineData(12)]
    public void Create_WithValidMonth_ShouldSucceed(int month)
    {
        var act = () => PurgeRecord.Create(
            userId:              UserId,
            periodYear:          2026,
            periodMonth:         month,
            totalIncome:         5000m,
            totalExpense:        3000m,
            incomeCount:         2,
            expenseCount:        10,
            categorySummaryJson: "{}",
            csvFileName:         "file.csv"
        );

        act.Should().NotThrow();
    }

    // ── Validação de ano ──────────────────────────────────────────────────────

    [Theory(DisplayName = "Deve lançar exceção para ano inválido")]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1899)]
    public void Create_WithInvalidYear_ShouldThrow(int year)
    {
        var act = () => PurgeRecord.Create(
            userId:              UserId,
            periodYear:          year,
            periodMonth:         4,
            totalIncome:         5000m,
            totalExpense:        3000m,
            incomeCount:         2,
            expenseCount:        10,
            categorySummaryJson: "{}",
            csvFileName:         "file.csv"
        );

        act.Should().Throw<DomainException>();
    }

    // ── Validação de totais ───────────────────────────────────────────────────

    [Theory(DisplayName = "Deve lançar exceção para TotalIncome negativo")]
    [InlineData(-1)]
    [InlineData(-0.01)]
    public void Create_WithNegativeTotalIncome_ShouldThrow(double totalIncome)
    {
        var act = () => PurgeRecord.Create(
            userId:              UserId,
            periodYear:          2026,
            periodMonth:         4,
            totalIncome:         (decimal)totalIncome,
            totalExpense:        3000m,
            incomeCount:         2,
            expenseCount:        10,
            categorySummaryJson: "{}",
            csvFileName:         "file.csv"
        );

        act.Should().Throw<DomainException>();
    }

    [Theory(DisplayName = "Deve lançar exceção para TotalExpense negativo")]
    [InlineData(-1)]
    [InlineData(-0.01)]
    public void Create_WithNegativeTotalExpense_ShouldThrow(double totalExpense)
    {
        var act = () => PurgeRecord.Create(
            userId:              UserId,
            periodYear:          2026,
            periodMonth:         4,
            totalIncome:         5000m,
            totalExpense:        (decimal)totalExpense,
            incomeCount:         2,
            expenseCount:        10,
            categorySummaryJson: "{}",
            csvFileName:         "file.csv"
        );

        act.Should().Throw<DomainException>();
    }

    [Fact(DisplayName = "Deve aceitar TotalIncome e TotalExpense zerados")]
    public void Create_WithZeroTotals_ShouldSucceed()
    {
        var act = () => PurgeRecord.Create(
            userId:              UserId,
            periodYear:          2026,
            periodMonth:         4,
            totalIncome:         0m,
            totalExpense:        0m,
            incomeCount:         0,
            expenseCount:        0,
            categorySummaryJson: "{}",
            csvFileName:         "file.csv"
        );

        act.Should().NotThrow();
    }

    // ── Validação de contagens ────────────────────────────────────────────────

    [Fact(DisplayName = "Deve lançar exceção para ExpenseCount negativo")]
    public void Create_WithNegativeExpenseCount_ShouldThrow()
    {
        var act = () => PurgeRecord.Create(
            userId:              UserId,
            periodYear:          2026,
            periodMonth:         4,
            totalIncome:         5000m,
            totalExpense:        3000m,
            incomeCount:         2,
            expenseCount:        -1,
            categorySummaryJson: "{}",
            csvFileName:         "file.csv"
        );

        act.Should().Throw<DomainException>();
    }

    [Fact(DisplayName = "Deve lançar exceção para IncomeCount negativo")]
    public void Create_WithNegativeIncomeCount_ShouldThrow()
    {
        var act = () => PurgeRecord.Create(
            userId:              UserId,
            periodYear:          2026,
            periodMonth:         4,
            totalIncome:         5000m,
            totalExpense:        3000m,
            incomeCount:         -1,
            expenseCount:        10,
            categorySummaryJson: "{}",
            csvFileName:         "file.csv"
        );

        act.Should().Throw<DomainException>();
    }

    // ── Validação de CsvFileName ──────────────────────────────────────────────

    [Theory(DisplayName = "Deve lançar exceção para CsvFileName vazio ou nulo")]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null)]
    public void Create_WithInvalidCsvFileName_ShouldThrow(string? csvFileName)
    {
        var act = () => PurgeRecord.Create(
            userId:              UserId,
            periodYear:          2026,
            periodMonth:         4,
            totalIncome:         5000m,
            totalExpense:        3000m,
            incomeCount:         2,
            expenseCount:        10,
            categorySummaryJson: "{}",
            csvFileName:         csvFileName!
        );

        act.Should().Throw<DomainException>();
    }

    // ── Validação de CategorySummaryJson ─────────────────────────────────────

    [Theory(DisplayName = "Deve lançar exceção para CategorySummaryJson vazio ou nulo")]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null)]
    public void Create_WithInvalidCategorySummaryJson_ShouldThrow(string? json)
    {
        var act = () => PurgeRecord.Create(
            userId:              UserId,
            periodYear:          2026,
            periodMonth:         4,
            totalIncome:         5000m,
            totalExpense:        3000m,
            incomeCount:         2,
            expenseCount:        10,
            categorySummaryJson: json!,
            csvFileName:         "file.csv"
        );

        act.Should().Throw<DomainException>();
    }
}
