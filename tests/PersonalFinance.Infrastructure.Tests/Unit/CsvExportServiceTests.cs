using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using PersonalFinance.Application.Interfaces;
using PersonalFinance.Domain.Entities.Config;
using PersonalFinance.Domain.Entities.Financial;
using PersonalFinance.Domain.Enums;
using PersonalFinance.Domain.Exceptions;
using PersonalFinance.Infrastructure.Persistence.Context;
using PersonalFinance.Infrastructure.Services;
using Xunit;

namespace PersonalFinance.Infrastructure.Tests.Unit;

/// <summary>
/// Testes unitários do CsvExportService (contrato do CSV).
/// CsvExportService ainda não existe — testes falham por comportamento ausente.
/// </summary>
public class CsvExportServiceTests
{
    // ── Header esperado do CSV ─────────────────────────────────────────────────
    private const string ExpectedHeader =
        "Type,PeriodYear,PeriodMonth,Description,Amount,Category,FortnightType,PaymentStatus,SourceType,DueDate,PaymentDate,Notes";

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"CsvExportTest_{Guid.NewGuid()}")
            .Options;
        return new AppDbContext(options);
    }

    /// <summary>
    /// Cria a SUT: CsvExportService recebe AppDbContext como dependência.
    /// A classe ainda não existe — referência resolve em tempo de compilação
    /// via stub abaixo; falha em runtime ao executar o comportamento real.
    /// </summary>
    private static ICsvExportService CreateSut(AppDbContext ctx)
        => new CsvExportService(ctx);

    private static Category CreateCategory(AppDbContext ctx)
    {
        var cat = Category.Create("Moradia", "#1E4D2B", null, Guid.NewGuid());
        ctx.Categories.Add(cat);
        ctx.SaveChanges();
        return cat;
    }

    private static (Guid userId, Guid periodId) SeedPeriod(
        AppDbContext ctx, int year = 2025, int month = 3)
    {
        var userId = Guid.NewGuid();
        var period = Period.Create(userId, year, month);
        ctx.Periods.Add(period);
        ctx.SaveChanges();
        return (userId, period.Id);
    }

    // ── Testes ────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "Exporta período com Income e Expense — CSV contém header correto")]
    public async Task ExportPeriodAsync_WithIncomeAndExpense_ShouldContainCorrectHeader()
    {
        using var ctx = CreateContext();
        var (userId, periodId) = SeedPeriod(ctx);
        var cat = CreateCategory(ctx);

        ctx.Incomes.Add(Income.Create(periodId, userId, FortnightType.First,
            "Salário", 5000m, new DateOnly(2025, 3, 5), null));

        ctx.Expenses.Add(Expense.Create(periodId, userId, cat.Id,
            SourceType.Personal, FortnightType.First, PaymentStatus.Paid,
            "Aluguel", 1200m, new DateOnly(2025, 3, 10),
            new DateOnly(2025, 3, 10), null));
        ctx.SaveChanges();

        var sut = CreateSut(ctx);

        var csv = await sut.ExportPeriodAsync(periodId, userId);

        // Remove BOM antes de verificar
        var csvClean = csv.TrimStart('﻿');
        var lines = csvClean.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        lines[0].Trim().Should().Be(ExpectedHeader);
    }

    [Fact(DisplayName = "Linha de Income tem campos exclusivos de Expense vazios")]
    public async Task ExportPeriodAsync_IncomeLine_ShouldHaveExpenseFieldsEmpty()
    {
        using var ctx = CreateContext();
        var (userId, periodId) = SeedPeriod(ctx);

        ctx.Incomes.Add(Income.Create(periodId, userId, FortnightType.First,
            "Salário", 5000m, new DateOnly(2025, 3, 5), "obs"));
        ctx.SaveChanges();

        var sut = CreateSut(ctx);
        var csv = await sut.ExportPeriodAsync(periodId, userId);

        var csvClean = csv.TrimStart('﻿');
        var lines = csvClean.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var incomeLine = lines.Skip(1).FirstOrDefault(l => l.StartsWith("Income"));
        incomeLine.Should().NotBeNull("deve haver uma linha de Income");

        var cols = incomeLine!.Split(',');
        // Formato: Type(0),PeriodYear(1),PeriodMonth(2),Description(3),Amount(4),Category(5),FortnightType(6),PaymentStatus(7),SourceType(8),DueDate(9),PaymentDate(10),Notes(11)
        cols[0].Should().Be("Income");
        cols[6].Should().BeEmpty("FortnightType é campo de Expense — vazio para Income");
        cols[7].Should().BeEmpty("PaymentStatus é campo de Expense — vazio para Income");
        cols[8].Should().BeEmpty("SourceType é campo de Expense — vazio para Income");
        cols[9].Should().BeEmpty("DueDate é campo de Expense — vazio para Income");
    }

    [Fact(DisplayName = "Linha de Expense tem SourceType preenchido")]
    public async Task ExportPeriodAsync_ExpenseLine_ShouldHaveSourceTypeFilled()
    {
        using var ctx = CreateContext();
        var (userId, periodId) = SeedPeriod(ctx);
        var cat = CreateCategory(ctx);

        ctx.Expenses.Add(Expense.Create(periodId, userId, cat.Id,
            SourceType.Personal, FortnightType.First, PaymentStatus.Pending,
            "Internet", 120m, new DateOnly(2025, 3, 20), null, null));
        ctx.SaveChanges();

        var sut = CreateSut(ctx);
        var csv = await sut.ExportPeriodAsync(periodId, userId);

        var csvClean = csv.TrimStart('﻿');
        var lines = csvClean.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var expenseLine = lines.Skip(1).FirstOrDefault(l => l.StartsWith("Expense"));
        expenseLine.Should().NotBeNull("deve haver uma linha de Expense");

        var cols = expenseLine!.Split(',');
        cols[0].Should().Be("Expense");
        cols[8].Should().NotBeEmpty("SourceType deve estar preenchido para Expense");
    }

    [Fact(DisplayName = "CSV inicia com BOM UTF-8 (EF BB BF)")]
    public async Task ExportPeriodAsync_ShouldStartWithUtf8Bom()
    {
        using var ctx = CreateContext();
        var (userId, periodId) = SeedPeriod(ctx);
        var sut = CreateSut(ctx);

        var csv = await sut.ExportPeriodAsync(periodId, userId);

        // Converte para bytes usando UTF-8 com BOM
        var bytes = System.Text.Encoding.UTF8.GetPreamble()
            .Concat(System.Text.Encoding.UTF8.GetBytes(csv.TrimStart('﻿')))
            .ToArray();

        // O CSV retornado como string deve começar com o char BOM U+FEFF
        csv[0].Should().Be('﻿', "CSV deve começar com BOM UTF-8");
    }

    [Fact(DisplayName = "Período apenas com Income exporta somente linhas de Income")]
    public async Task ExportPeriodAsync_OnlyIncome_ShouldExportOnlyIncomeLines()
    {
        using var ctx = CreateContext();
        var (userId, periodId) = SeedPeriod(ctx);

        ctx.Incomes.Add(Income.Create(periodId, userId, FortnightType.First,
            "Freelance", 3000m, new DateOnly(2025, 3, 1), null));
        ctx.Incomes.Add(Income.Create(periodId, userId, FortnightType.Second,
            "Salário", 5000m, new DateOnly(2025, 3, 15), null));
        ctx.SaveChanges();

        var sut = CreateSut(ctx);
        var csv = await sut.ExportPeriodAsync(periodId, userId);

        var csvClean = csv.TrimStart('﻿');
        var lines = csvClean.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        lines.Where(l => l.StartsWith("Income")).Should().HaveCount(2);
        lines.Where(l => l.StartsWith("Expense")).Should().BeEmpty();
    }

    [Fact(DisplayName = "Período apenas com Expense exporta somente linhas de Expense")]
    public async Task ExportPeriodAsync_OnlyExpense_ShouldExportOnlyExpenseLines()
    {
        using var ctx = CreateContext();
        var (userId, periodId) = SeedPeriod(ctx);
        var cat = CreateCategory(ctx);

        ctx.Expenses.Add(Expense.Create(periodId, userId, cat.Id,
            SourceType.Personal, FortnightType.First, PaymentStatus.Pending,
            "Aluguel", 1500m, new DateOnly(2025, 3, 10), null, null));
        ctx.SaveChanges();

        var sut = CreateSut(ctx);
        var csv = await sut.ExportPeriodAsync(periodId, userId);

        var csvClean = csv.TrimStart('﻿');
        var lines = csvClean.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        lines.Where(l => l.StartsWith("Expense")).Should().HaveCount(1);
        lines.Where(l => l.StartsWith("Income")).Should().BeEmpty();
    }

    [Fact(DisplayName = "Período sem registros retorna apenas o header")]
    public async Task ExportPeriodAsync_EmptyPeriod_ShouldReturnOnlyHeader()
    {
        using var ctx = CreateContext();
        var (userId, periodId) = SeedPeriod(ctx);
        var sut = CreateSut(ctx);

        var csv = await sut.ExportPeriodAsync(periodId, userId);

        var csvClean = csv.TrimStart('﻿');
        var lines = csvClean.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        lines.Should().HaveCount(1);
        lines[0].Trim().Should().Be(ExpectedHeader);
    }

    [Fact(DisplayName = "PeriodId de outro usuário lança DomainException")]
    public async Task ExportPeriodAsync_WrongUser_ShouldThrowDomainException()
    {
        using var ctx = CreateContext();
        var (_, periodId) = SeedPeriod(ctx);
        var outroUserId = Guid.NewGuid();
        var sut = CreateSut(ctx);

        var act = async () => await sut.ExportPeriodAsync(periodId, outroUserId);

        await act.Should().ThrowAsync<DomainException>();
    }
}
