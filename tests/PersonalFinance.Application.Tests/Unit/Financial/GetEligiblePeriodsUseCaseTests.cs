using FluentAssertions;
using Moq;
using PersonalFinance.Application.DTOs.Financial;
using PersonalFinance.Application.UseCases.Financial.Purge;
using PersonalFinance.Domain.Entities.Financial;
using PersonalFinance.Domain.Enums;
using PersonalFinance.Domain.Interfaces.Repositories;
using Xunit;

namespace PersonalFinance.Application.Tests.Unit.Financial;

/// <summary>
/// Testes do GetEligiblePeriodsUseCase.
/// Regras: retornar apenas períodos inativos do usuário; agregar totais de receita,
/// despesa e contagem de itens por período; nunca vazar dados de outro usuário.
/// </summary>
public class GetEligiblePeriodsUseCaseTests
{
    private readonly Mock<IPeriodRepository>  _periodRepo  = new();
    private readonly Mock<IExpenseRepository> _expenseRepo = new();
    private readonly Mock<IIncomeRepository>  _incomeRepo  = new();
    private readonly GetEligiblePeriodsUseCase _sut;

    private static readonly Guid UserId  = Guid.NewGuid();
    private static readonly Guid UserId2 = Guid.NewGuid();

    public GetEligiblePeriodsUseCaseTests()
    {
        _sut = new GetEligiblePeriodsUseCase(
            _periodRepo.Object,
            _expenseRepo.Object,
            _incomeRepo.Object);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Period BuildInactivePeriod(Guid userId, int year, int month)
    {
        var p = Period.Create(userId, year, month);
        p.Deactivate();
        return p;
    }

    private static Expense BuildExpense(Guid periodId, Guid userId, decimal amount) =>
        Expense.Create(
            periodId:      periodId,
            userId:        userId,
            categoryId:    Guid.NewGuid(),
            sourceType:    SourceType.Personal,
            fortnightType: FortnightType.First,
            paymentStatus: PaymentStatus.Pending,
            description:   "Despesa teste",
            amount:        amount,
            dueDate:       DateOnly.FromDateTime(DateTime.UtcNow),
            paymentDate:   null,
            notes:         null);

    private static Income BuildIncome(Guid periodId, Guid userId, decimal amount) =>
        Income.Create(
            periodId:      periodId,
            userId:        userId,
            fortnightType: FortnightType.First,
            description:   "Receita teste",
            amount:        amount,
            receivedAt:    DateOnly.FromDateTime(DateTime.UtcNow),
            notes:         null);

    // ── Caminho feliz ─────────────────────────────────────────────────────────

    [Fact(DisplayName = "Deve retornar lista com todos os campos corretos para o usuário")]
    public async Task Execute_ReturnsEligiblePeriods_WithAllFields()
    {
        var period   = BuildInactivePeriod(UserId, 2026, 3);
        var periodId = period.Id;

        var expenses = new[] { BuildExpense(periodId, UserId, 1500m), BuildExpense(periodId, UserId, 500m) };
        var incomes  = new[] { BuildIncome(periodId, UserId, 5000m) };

        _periodRepo.Setup(r => r.GetByUserAsync(UserId, default))
                   .ReturnsAsync(new[] { period });
        _expenseRepo.Setup(r => r.GetByPeriodAsync(periodId, UserId, default))
                    .ReturnsAsync(expenses);
        _incomeRepo.Setup(r => r.GetByPeriodAsync(periodId, UserId, default))
                   .ReturnsAsync(incomes);

        var result = (await _sut.ExecuteAsync(UserId)).ToList();

        result.Should().HaveCount(1);

        var dto = result[0];
        dto.PeriodId.Should().Be(periodId);
        dto.Year.Should().Be(2026);
        dto.Month.Should().Be(3);
        dto.TotalExpense.Should().Be(2000m);
        dto.TotalIncome.Should().Be(5000m);
        dto.ItemCount.Should().Be(3);
    }

    // ── Filtro por userId ─────────────────────────────────────────────────────

    [Fact(DisplayName = "Deve retornar apenas períodos do userId1 — dados de userId2 não aparecem")]
    public async Task Execute_ReturnsOnlyPeriodsForUser()
    {
        var periodUser1 = BuildInactivePeriod(UserId, 2026, 1);
        var periodUser2 = BuildInactivePeriod(UserId2, 2026, 1);

        // userId1 possui um período inativo
        _periodRepo.Setup(r => r.GetByUserAsync(UserId, default))
                   .ReturnsAsync(new[] { periodUser1 });
        _expenseRepo.Setup(r => r.GetByPeriodAsync(periodUser1.Id, UserId, default))
                    .ReturnsAsync(Array.Empty<Expense>());
        _incomeRepo.Setup(r => r.GetByPeriodAsync(periodUser1.Id, UserId, default))
                   .ReturnsAsync(Array.Empty<Income>());

        // userId2 possui um período inativo (mas não deve aparecer no resultado de userId1)
        _periodRepo.Setup(r => r.GetByUserAsync(UserId2, default))
                   .ReturnsAsync(new[] { periodUser2 });

        var result = (await _sut.ExecuteAsync(UserId)).ToList();

        // Apenas o período de UserId deve aparecer
        result.Should().HaveCount(1);
        result[0].PeriodId.Should().Be(periodUser1.Id);
        result.Should().NotContain(dto => dto.PeriodId == periodUser2.Id);

        // Nunca deve consultar repositórios com o userId2
        _periodRepo.Verify(r => r.GetByUserAsync(UserId2, default), Times.Never);
    }

    // ── Período ativo excluído ────────────────────────────────────────────────

    [Fact(DisplayName = "Não deve retornar períodos ativos — apenas inativos são elegíveis")]
    public async Task Execute_ExcludesActivePeriods()
    {
        var activePeriod   = Period.Create(UserId, 2026, 4); // IsActive = true por padrão
        var inactivePeriod = BuildInactivePeriod(UserId, 2026, 3);

        _periodRepo.Setup(r => r.GetByUserAsync(UserId, default))
                   .ReturnsAsync(new[] { activePeriod, inactivePeriod });
        _expenseRepo.Setup(r => r.GetByPeriodAsync(inactivePeriod.Id, UserId, default))
                    .ReturnsAsync(Array.Empty<Expense>());
        _incomeRepo.Setup(r => r.GetByPeriodAsync(inactivePeriod.Id, UserId, default))
                   .ReturnsAsync(Array.Empty<Income>());

        var result = (await _sut.ExecuteAsync(UserId)).ToList();

        result.Should().HaveCount(1);
        result[0].PeriodId.Should().Be(inactivePeriod.Id);
        result.Should().NotContain(dto => dto.PeriodId == activePeriod.Id);
    }

    // ── Lista vazia ───────────────────────────────────────────────────────────

    [Fact(DisplayName = "Deve retornar lista vazia se não houver períodos elegíveis")]
    public async Task Execute_ReturnsEmpty_WhenNoEligiblePeriods()
    {
        _periodRepo.Setup(r => r.GetByUserAsync(UserId, default))
                   .ReturnsAsync(Array.Empty<Period>());

        var result = await _sut.ExecuteAsync(UserId);

        result.Should().BeEmpty();
    }

    [Fact(DisplayName = "Deve retornar lista vazia quando todos os períodos são ativos")]
    public async Task Execute_ReturnsEmpty_WhenAllPeriodsAreActive()
    {
        var activePeriod = Period.Create(UserId, 2026, 4); // IsActive = true por padrão

        _periodRepo.Setup(r => r.GetByUserAsync(UserId, default))
                   .ReturnsAsync(new[] { activePeriod });

        var result = await _sut.ExecuteAsync(UserId);

        result.Should().BeEmpty();
    }

    // ── CancellationToken propagado ───────────────────────────────────────────

    [Fact(DisplayName = "Deve propagar o CancellationToken a todos os repositórios")]
    public async Task Execute_ShouldPropagateCancellationToken()
    {
        var cts    = new CancellationTokenSource();
        var ct     = cts.Token;
        var period = BuildInactivePeriod(UserId, 2026, 2);

        _periodRepo.Setup(r => r.GetByUserAsync(UserId, ct))
                   .ReturnsAsync(new[] { period });
        _expenseRepo.Setup(r => r.GetByPeriodAsync(period.Id, UserId, ct))
                    .ReturnsAsync(Array.Empty<Expense>());
        _incomeRepo.Setup(r => r.GetByPeriodAsync(period.Id, UserId, ct))
                   .ReturnsAsync(Array.Empty<Income>());

        await _sut.ExecuteAsync(UserId, ct);

        _periodRepo.Verify(r  => r.GetByUserAsync(UserId, ct),            Times.Once);
        _expenseRepo.Verify(r => r.GetByPeriodAsync(period.Id, UserId, ct), Times.Once);
        _incomeRepo.Verify(r  => r.GetByPeriodAsync(period.Id, UserId, ct), Times.Once);
    }
}
