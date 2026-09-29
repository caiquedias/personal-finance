using FluentAssertions;
using Moq;
using PersonalFinance.Application.DTOs.Import;
using PersonalFinance.Application.Interfaces;
using PersonalFinance.Application.UseCases.Import;
using PersonalFinance.Domain.Entities.Config;
using PersonalFinance.Domain.Entities.Financial;
using PersonalFinance.Domain.Enums;
using PersonalFinance.Domain.Exceptions;
using PersonalFinance.Domain.Interfaces.Repositories;
using Xunit;

namespace PersonalFinance.Application.Tests.Unit.Import;

public class PreviewStatementImportUseCaseTests
{
    private readonly Mock<IStatementParserService> _parser     = new();
    private readonly Mock<ICategoryRepository>     _catRepo    = new();
    private readonly Mock<IPeriodRepository>       _periodRepo = new();
    private readonly Mock<IExpenseRepository>      _expRepo    = new();
    private readonly Mock<IIncomeRepository>       _incRepo    = new();
    private readonly PreviewStatementImportUseCase _sut;

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateOnly D  = new(2026, 3, 10);

    public PreviewStatementImportUseCaseTests()
    {
        _catRepo.Setup(r => r.GetByUserAsync(It.IsAny<Guid>(), default))
                .ReturnsAsync(Array.Empty<Category>());
        _periodRepo.Setup(r => r.GetByUserYearMonthAsync(
                It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>(), default))
                .ReturnsAsync((Period?)null);
        _expRepo.Setup(r => r.GetByPeriodAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), default))
                .ReturnsAsync(Array.Empty<Expense>());
        _incRepo.Setup(r => r.GetByPeriodAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), default))
                .ReturnsAsync(Array.Empty<Income>());

        _sut = new PreviewStatementImportUseCase(
            _parser.Object, _catRepo.Object, _periodRepo.Object,
            _expRepo.Object, _incRepo.Object, new StatementEntryClassifier());
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static ParsedStatementEntryDto Entry(decimal amount, string desc = "LOJA XYZ", DateOnly? posting = null) =>
        new(posting ?? D, posting ?? D, "Débito", desc, amount);

    private void SetupParser(params ParsedStatementEntryDto[] entries) =>
        _parser.Setup(p => p.ParseAsync(It.IsAny<Stream>(), It.IsAny<string?>(), default))
               .ReturnsAsync(entries);

    private Task<StatementPreviewResultDto> Run(DateOnly? from = null, string? password = null) =>
        _sut.ExecuteAsync(Stream.Null, password, from ?? new DateOnly(2026, 1, 1), UserId);

    private Period SetupPeriod(int year, int month, Guid userId)
    {
        var period = Period.Create(userId, year, month);
        _periodRepo.Setup(r => r.GetByUserYearMonthAsync(userId, year, month, default))
                   .ReturnsAsync(period);
        return period;
    }

    private static Expense BuildExpense(Guid periodId, string desc, decimal amount, DateOnly due, DateOnly? paid = null) =>
        Expense.Create(periodId, UserId, Guid.NewGuid(), SourceType.Personal,
            FortnightType.First, PaymentStatus.Pending, desc, amount, due, paid, null);

    // ── Classificação ─────────────────────────────────────────────────────────

    [Fact(DisplayName = "Valor positivo vira Income e negativo vira Expense com valor absoluto")]
    public async Task Execute_SignDefinesKind_AmountIsAbsolute()
    {
        SetupParser(Entry(150.50m, "PIX RECEBIDO"), Entry(-42.30m, "LOJA XYZ"));

        var result = await Run();

        result.Items.Should().HaveCount(2);
        var income  = result.Items.Single(i => i.Description == "PIX RECEBIDO");
        var expense = result.Items.Single(i => i.Description == "LOJA XYZ");
        income.Kind.Should().Be("Income");
        income.Amount.Should().Be(150.50m);
        expense.Kind.Should().Be("Expense");
        expense.Amount.Should().Be(42.30m);
        income.Date.Should().Be(D);
    }

    [Fact(DisplayName = "Lançamento com valor zero deve ser descartado")]
    public async Task Execute_ZeroAmount_ShouldBeDiscarded()
    {
        SetupParser(Entry(0m), Entry(-10m));

        var result = await Run();

        result.Items.Should().ContainSingle();
    }

    [Fact(DisplayName = "Deve repassar a senha ao parser")]
    public async Task Execute_ShouldPassPasswordToParser()
    {
        SetupParser();

        await Run(password: "123456");

        _parser.Verify(p => p.ParseAsync(It.IsAny<Stream>(), "123456", default), Times.Once);
    }

    [Fact(DisplayName = "Deve propagar DomainException do parser")]
    public async Task Execute_ParserThrows_ShouldPropagate()
    {
        _parser.Setup(p => p.ParseAsync(It.IsAny<Stream>(), It.IsAny<string?>(), default))
               .ThrowsAsync(new DomainException("Senha incorreta."));

        var act = () => Run();

        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact(DisplayName = "Extrato vazio deve retornar lista vazia")]
    public async Task Execute_EmptyStatement_ShouldReturnEmpty()
    {
        SetupParser();

        var result = await Run();

        result.Items.Should().BeEmpty();
    }

    // ── fromDate ──────────────────────────────────────────────────────────────

    [Fact(DisplayName = "Deve descartar anteriores a fromDate e manter a data-limite exata")]
    public async Task Execute_FromDate_BoundaryIsInclusive()
    {
        var limit = new DateOnly(2026, 3, 10);
        SetupParser(
            Entry(-1m, "ANTES",  limit.AddDays(-1)),
            Entry(-2m, "IGUAL",  limit),
            Entry(-3m, "DEPOIS", limit.AddDays(1)));

        var result = await Run(from: limit);

        result.Items.Select(i => i.Description).Should().BeEquivalentTo(new[] { "IGUAL", "DEPOIS" });
        result.DiscardedByDateCount.Should().Be(1);
    }

    [Fact(DisplayName = "fromDate futuro deve retornar lista vazia")]
    public async Task Execute_FutureFromDate_ShouldReturnEmpty()
    {
        SetupParser(Entry(-5m));

        var result = await Run(from: new DateOnly(2999, 1, 1));

        result.Items.Should().BeEmpty();
    }

    [Fact(DisplayName = "Filtro fromDate usa PostingDate e não EventDate")]
    public async Task Execute_FromDate_UsesPostingDate()
    {
        var entry = new ParsedStatementEntryDto(
            new DateOnly(2026, 1, 1), new DateOnly(2026, 3, 20), "Débito", "LOJA", -5m);
        SetupParser(entry);

        var result = await Run(from: new DateOnly(2026, 3, 1));

        result.Items.Should().ContainSingle().Which.Date.Should().Be(new DateOnly(2026, 3, 20));
    }

    // ── Transferência interna ─────────────────────────────────────────────────

    [Fact(DisplayName = "Deve sinalizar transferência interna")]
    public async Task Execute_InternalTransfer_ShouldBeFlagged()
    {
        SetupParser(Entry(-500m, "PGTO FAT CARTAO C6"), Entry(-20m, "LOJA XYZ"));

        var result = await Run();

        result.Items.Single(i => i.Description.StartsWith("PGTO")).IsLikelyInternalTransfer.Should().BeTrue();
        result.Items.Single(i => i.Description == "LOJA XYZ").IsLikelyInternalTransfer.Should().BeFalse();
    }

    // ── Categoria ─────────────────────────────────────────────────────────────

    [Fact(DisplayName = "Deve sugerir categoria acessível ao usuário e null sem correspondência")]
    public async Task Execute_Category_SuggestedOrNull()
    {
        var transport = Category.CreateGlobal("Transporte", "#111111", null);
        _catRepo.Setup(r => r.GetByUserAsync(UserId, default)).ReturnsAsync(new[] { transport });
        SetupParser(Entry(-20m, "UBER TRIP"), Entry(-20m, "LOJA XYZ"));

        var result = await Run();

        result.Items.Single(i => i.Description == "UBER TRIP").SuggestedCategoryId.Should().Be(transport.Id);
        result.Items.Single(i => i.Description == "LOJA XYZ").SuggestedCategoryId.Should().BeNull();
        _catRepo.Verify(r => r.GetByUserAsync(UserId, default), Times.AtLeastOnce);
    }

    [Fact(DisplayName = "Categoria de outro usuário nunca é sugerida")]
    public async Task Execute_OtherUserCategory_NeverSuggested()
    {
        var otherUserCat = Category.Create("Transporte", "#111111", null, Guid.NewGuid());
        _catRepo.Setup(r => r.GetByUserAsync(UserId, default)).ReturnsAsync(Array.Empty<Category>());
        _catRepo.Setup(r => r.GetByUserAsync(It.Is<Guid>(g => g != UserId), default))
                .ReturnsAsync(new[] { otherUserCat });
        SetupParser(Entry(-20m, "UBER TRIP"));

        var result = await Run();

        result.Items.Single().SuggestedCategoryId.Should().BeNull();
    }

    // ── Duplicatas ────────────────────────────────────────────────────────────

    [Fact(DisplayName = "Deve marcar duplicata de Expense (data, valor absoluto e descrição case-insensitive)")]
    public async Task Execute_DuplicateExpense_ShouldBeFlagged()
    {
        var period   = SetupPeriod(2026, 3, UserId);
        var existing = BuildExpense(period.Id, "loja xyz", 42.30m, D);
        _expRepo.Setup(r => r.GetByPeriodAsync(period.Id, UserId, default))
                .ReturnsAsync(new[] { existing });
        SetupParser(Entry(-42.30m, "LOJA XYZ"));

        var result = await Run();

        var item = result.Items.Single();
        item.IsLikelyDuplicate.Should().BeTrue();
        item.DuplicateOfId.Should().Be(existing.Id);
    }

    [Fact(DisplayName = "Duplicata de Expense compara PaymentDate quando preenchida")]
    public async Task Execute_DuplicateExpense_UsesPaymentDateOverDueDate()
    {
        var period   = SetupPeriod(2026, 3, UserId);
        var paid     = new DateOnly(2026, 3, 10);
        var existing = BuildExpense(period.Id, "LOJA XYZ", 42.30m, new DateOnly(2026, 3, 25), paid);
        _expRepo.Setup(r => r.GetByPeriodAsync(period.Id, UserId, default))
                .ReturnsAsync(new[] { existing });
        SetupParser(Entry(-42.30m, "LOJA XYZ", paid));

        var result = await Run();

        result.Items.Single().IsLikelyDuplicate.Should().BeTrue();
    }

    [Fact(DisplayName = "Deve marcar duplicata de Income")]
    public async Task Execute_DuplicateIncome_ShouldBeFlagged()
    {
        var period   = SetupPeriod(2026, 3, UserId);
        var existing = Income.Create(period.Id, UserId, FortnightType.First, "Salario", 3000m, D, null);
        _incRepo.Setup(r => r.GetByPeriodAsync(period.Id, UserId, default))
                .ReturnsAsync(new[] { existing });
        SetupParser(Entry(3000m, "SALARIO"));

        var result = await Run();

        var item = result.Items.Single();
        item.Kind.Should().Be("Income");
        item.IsLikelyDuplicate.Should().BeTrue();
        item.DuplicateOfId.Should().Be(existing.Id);
    }

    [Fact(DisplayName = "Data diferente não é duplicata")]
    public async Task Execute_DifferentDate_NotDuplicate()
    {
        var period   = SetupPeriod(2026, 3, UserId);
        var existing = BuildExpense(period.Id, "LOJA XYZ", 42.30m, D.AddDays(1));
        _expRepo.Setup(r => r.GetByPeriodAsync(period.Id, UserId, default))
                .ReturnsAsync(new[] { existing });
        SetupParser(Entry(-42.30m, "LOJA XYZ"));

        var result = await Run();

        var item = result.Items.Single();
        item.IsLikelyDuplicate.Should().BeFalse();
        item.DuplicateOfId.Should().BeNull();
    }

    [Fact(DisplayName = "Valor diferente não é duplicata")]
    public async Task Execute_DifferentAmount_NotDuplicate()
    {
        var period   = SetupPeriod(2026, 3, UserId);
        var existing = BuildExpense(period.Id, "LOJA XYZ", 50m, D);
        _expRepo.Setup(r => r.GetByPeriodAsync(period.Id, UserId, default))
                .ReturnsAsync(new[] { existing });
        SetupParser(Entry(-42.30m, "LOJA XYZ"));

        var result = await Run();

        result.Items.Single().IsLikelyDuplicate.Should().BeFalse();
    }

    [Fact(DisplayName = "Registro de outro usuário não gera duplicata (consulta filtra por userId)")]
    public async Task Execute_OtherUserRecord_NotDuplicate()
    {
        var otherUser   = Guid.NewGuid();
        var otherPeriod = Period.Create(otherUser, 2026, 3);
        var otherExp = Expense.Create(otherPeriod.Id, otherUser, Guid.NewGuid(), SourceType.Personal,
            FortnightType.First, PaymentStatus.Pending, "LOJA XYZ", 42.30m, D, null, null);
        _periodRepo.Setup(r => r.GetByUserYearMonthAsync(otherUser, 2026, 3, default)).ReturnsAsync(otherPeriod);
        _expRepo.Setup(r => r.GetByPeriodAsync(otherPeriod.Id, otherUser, default)).ReturnsAsync(new[] { otherExp });
        SetupParser(Entry(-42.30m, "LOJA XYZ"));

        var result = await Run();

        result.Items.Single().IsLikelyDuplicate.Should().BeFalse();
        _periodRepo.Verify(r => r.GetByUserYearMonthAsync(UserId, 2026, 3, default), Times.AtLeastOnce);
        _periodRepo.Verify(r => r.GetByUserYearMonthAsync(otherUser, It.IsAny<int>(), It.IsAny<int>(), default), Times.Never);
    }

    [Fact(DisplayName = "Mês sem período existente não é duplicata")]
    public async Task Execute_NoPeriodForMonth_NotDuplicate()
    {
        SetupParser(Entry(-42.30m, "LOJA XYZ"));

        var result = await Run();

        result.Items.Single().IsLikelyDuplicate.Should().BeFalse();
        _expRepo.Verify(r => r.GetByPeriodAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), default), Times.Never);
    }

    // ── Preview puro (sem persistência) ───────────────────────────────────────

    [Fact(DisplayName = "Preview nunca persiste dados")]
    public async Task Execute_ShouldNeverPersist()
    {
        SetupPeriod(2026, 3, UserId);
        SetupParser(Entry(-42.30m), Entry(100m, "PIX RECEBIDO"));

        await Run();

        _expRepo.Verify(r => r.AddAsync(It.IsAny<Expense>(), default), Times.Never);
        _expRepo.Verify(r => r.AddRangeAsync(It.IsAny<IEnumerable<Expense>>(), default), Times.Never);
        _expRepo.Verify(r => r.UpdateAsync(It.IsAny<Expense>(), default), Times.Never);
        _incRepo.Verify(r => r.AddAsync(It.IsAny<Income>(), default), Times.Never);
        _incRepo.Verify(r => r.UpdateAsync(It.IsAny<Income>(), default), Times.Never);
        _periodRepo.Verify(r => r.AddAsync(It.IsAny<Period>(), default), Times.Never);
        _catRepo.Verify(r => r.AddAsync(It.IsAny<Category>(), default), Times.Never);
    }
}
