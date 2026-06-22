using FluentAssertions;
using Moq;
using PersonalFinance.Application.UseCases.Financial.Purge;
using PersonalFinance.Domain.Entities.Financial;
using PersonalFinance.Domain.Enums;
using PersonalFinance.Domain.Exceptions;
using PersonalFinance.Domain.Interfaces.Repositories;
using Xunit;

namespace PersonalFinance.Application.Tests.Unit.Financial;

/// <summary>
/// Testes do PurgePeriodUseCase.
/// Regras: valida ownership; insere PurgeRecord; deleta fisicamente Incomes, Expenses e Period;
/// tudo em transação única; rollback em falha.
/// </summary>
public class PurgePeriodUseCaseTests
{
    private readonly Mock<IPeriodRepository>  _periodRepo  = new();
    private readonly Mock<IExpenseRepository> _expenseRepo = new();
    private readonly Mock<IIncomeRepository>  _incomeRepo  = new();
    private readonly Mock<IPurgeRepository>   _purgeRepo   = new();
    private readonly Mock<IUnitOfWork>        _uow         = new();
    private readonly PurgePeriodUseCase       _sut;

    private static readonly Guid UserId     = Guid.NewGuid();
    private static readonly Guid PeriodId   = Guid.NewGuid();
    private static readonly Guid CategoryId = Guid.NewGuid();

    public PurgePeriodUseCaseTests()
    {
        _sut = new PurgePeriodUseCase(
            _periodRepo.Object,
            _expenseRepo.Object,
            _incomeRepo.Object,
            _purgeRepo.Object,
            _uow.Object);
    }

    private static Expense BuildExpense(decimal amount = 500m) =>
        Expense.Create(PeriodId, UserId, CategoryId,
            SourceType.Personal, FortnightType.First, PaymentStatus.Pending,
            "Aluguel", amount, new DateOnly(2026, 4, 10), null, null);

    private static Income BuildIncome(decimal amount = 3000m) =>
        Income.Create(PeriodId, UserId, FortnightType.First,
            "Salário", amount, new DateOnly(2026, 4, 5), null);

    // ── Caminho feliz ─────────────────────────────────────────────────────────

    [Fact(DisplayName = "Deve criar PurgeRecord com totais corretos e deletar tudo")]
    public async Task Execute_ValidPeriod_ShouldPurgeAndCommit()
    {
        var period   = Period.Create(UserId, 2026, 4);
        var expenses = new[] { BuildExpense(500m), BuildExpense(200m) };
        var incomes  = new[] { BuildIncome(3000m), BuildIncome(1500m) };

        _periodRepo.Setup(r => r.GetByIdAndUserAsync(PeriodId, UserId, default))
                   .ReturnsAsync(period);
        _expenseRepo.Setup(r => r.GetByPeriodAsync(PeriodId, UserId, default))
                    .ReturnsAsync(expenses);
        _incomeRepo.Setup(r => r.GetByPeriodAsync(PeriodId, UserId, default))
                   .ReturnsAsync(incomes);

        await _sut.ExecuteAsync(PeriodId, UserId);

        // Deve inserir 1 PurgeRecord
        _purgeRepo.Verify(r => r.AddAsync(
            It.Is<PurgeRecord>(pr =>
                pr.TotalExpense   == 700m  &&
                pr.TotalIncome    == 4500m &&
                pr.ExpenseCount   == 2     &&
                pr.IncomeCount    == 2     &&
                pr.UserId         == UserId),
            default), Times.Once);

        // Deve commitar a transação
        _uow.Verify(u => u.CommitAsync(default), Times.Once);
    }

    [Fact(DisplayName = "Deve calcular CategorySummaryJson com totais por categoria")]
    public async Task Execute_MultipleExpenseCategories_ShouldCalculateSummaryJson()
    {
        var period  = Period.Create(UserId, 2026, 4);
        var expense = BuildExpense(500m);

        _periodRepo.Setup(r => r.GetByIdAndUserAsync(PeriodId, UserId, default))
                   .ReturnsAsync(period);
        _expenseRepo.Setup(r => r.GetByPeriodAsync(PeriodId, UserId, default))
                    .ReturnsAsync(new[] { expense });
        _incomeRepo.Setup(r => r.GetByPeriodAsync(PeriodId, UserId, default))
                   .ReturnsAsync(Enumerable.Empty<Income>());

        await _sut.ExecuteAsync(PeriodId, UserId);

        _purgeRepo.Verify(r => r.AddAsync(
            It.Is<PurgeRecord>(pr =>
                !string.IsNullOrEmpty(pr.CategorySummaryJson)),
            default), Times.Once);
    }

    [Fact(DisplayName = "Deve gerar CsvFileName com ano e mês do período")]
    public async Task Execute_ValidPeriod_ShouldGenerateCsvFileName()
    {
        var period = Period.Create(UserId, 2026, 4);

        _periodRepo.Setup(r => r.GetByIdAndUserAsync(PeriodId, UserId, default))
                   .ReturnsAsync(period);
        _expenseRepo.Setup(r => r.GetByPeriodAsync(PeriodId, UserId, default))
                    .ReturnsAsync(Enumerable.Empty<Expense>());
        _incomeRepo.Setup(r => r.GetByPeriodAsync(PeriodId, UserId, default))
                   .ReturnsAsync(Enumerable.Empty<Income>());

        await _sut.ExecuteAsync(PeriodId, UserId);

        _purgeRepo.Verify(r => r.AddAsync(
            It.Is<PurgeRecord>(pr =>
                pr.CsvFileName.Contains("2026") &&
                pr.CsvFileName.Contains("04")),
            default), Times.Once);
    }

    // ── Período não encontrado ────────────────────────────────────────────────

    [Fact(DisplayName = "Deve lançar exceção quando período não encontrado")]
    public async Task Execute_PeriodNotFound_ShouldThrow()
    {
        _periodRepo.Setup(r => r.GetByIdAndUserAsync(PeriodId, UserId, default))
                   .ReturnsAsync((Period?)null);

        var act = () => _sut.ExecuteAsync(PeriodId, UserId);

        await act.Should().ThrowAsync<DomainException>();
        _purgeRepo.Verify(r => r.AddAsync(It.IsAny<PurgeRecord>(), default), Times.Never);
        _uow.Verify(u => u.CommitAsync(default), Times.Never);
    }

    // ── Rollback em falha ─────────────────────────────────────────────────────

    [Fact(DisplayName = "Não deve commitar se AddAsync do PurgeRecord lançar exceção")]
    public async Task Execute_PurgeRepoThrows_ShouldNotCommit()
    {
        var period = Period.Create(UserId, 2026, 4);

        _periodRepo.Setup(r => r.GetByIdAndUserAsync(PeriodId, UserId, default))
                   .ReturnsAsync(period);
        _expenseRepo.Setup(r => r.GetByPeriodAsync(PeriodId, UserId, default))
                    .ReturnsAsync(Enumerable.Empty<Expense>());
        _incomeRepo.Setup(r => r.GetByPeriodAsync(PeriodId, UserId, default))
                   .ReturnsAsync(Enumerable.Empty<Income>());
        _purgeRepo.Setup(r => r.AddAsync(It.IsAny<PurgeRecord>(), default))
                  .ThrowsAsync(new InvalidOperationException("Erro no banco"));

        var act = () => _sut.ExecuteAsync(PeriodId, UserId);

        await act.Should().ThrowAsync<InvalidOperationException>();
        _uow.Verify(u => u.CommitAsync(default), Times.Never);
    }

    // ── Período sem lançamentos ───────────────────────────────────────────────

    [Fact(DisplayName = "Deve expurgar período vazio (sem despesas e receitas)")]
    public async Task Execute_EmptyPeriod_ShouldSucceed()
    {
        var period = Period.Create(UserId, 2026, 4);

        _periodRepo.Setup(r => r.GetByIdAndUserAsync(PeriodId, UserId, default))
                   .ReturnsAsync(period);
        _expenseRepo.Setup(r => r.GetByPeriodAsync(PeriodId, UserId, default))
                    .ReturnsAsync(Enumerable.Empty<Expense>());
        _incomeRepo.Setup(r => r.GetByPeriodAsync(PeriodId, UserId, default))
                   .ReturnsAsync(Enumerable.Empty<Income>());

        var act = () => _sut.ExecuteAsync(PeriodId, UserId);

        await act.Should().NotThrowAsync();
        _purgeRepo.Verify(r => r.AddAsync(
            It.Is<PurgeRecord>(pr => pr.ExpenseCount == 0 && pr.IncomeCount == 0),
            default), Times.Once);
        _uow.Verify(u => u.CommitAsync(default), Times.Once);
    }
}
