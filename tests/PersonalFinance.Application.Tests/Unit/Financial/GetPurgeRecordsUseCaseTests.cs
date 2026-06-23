using FluentAssertions;
using Moq;
using PersonalFinance.Application.UseCases.Financial.Purge;
using PersonalFinance.Domain.Entities.Financial;
using PersonalFinance.Domain.Interfaces.Repositories;
using Xunit;

namespace PersonalFinance.Application.Tests.Unit.Financial;

/// <summary>
/// Testes do GetPurgeRecordsUseCase.
/// Regras: filtro obrigatório por userId; ordenação decrescente por PurgedAt.
/// </summary>
public class GetPurgeRecordsUseCaseTests
{
    private readonly Mock<IPurgeRepository>  _purgeRepo = new();
    private readonly GetPurgeRecordsUseCase  _sut;

    private static readonly Guid UserId = Guid.NewGuid();

    public GetPurgeRecordsUseCaseTests()
    {
        _sut = new GetPurgeRecordsUseCase(_purgeRepo.Object);
    }

    private static PurgeRecord BuildRecord(short year, byte month) =>
        PurgeRecord.Create(
            userId:              UserId,
            periodYear:          year,
            periodMonth:         month,
            totalIncome:         5000m,
            totalExpense:        3000m,
            incomeCount:         2,
            expenseCount:        10,
            categorySummaryJson: "{}",
            csvFileName:         $"expurgo_{year}_{month:D2}.csv"
        );

    // ── Caminho feliz ─────────────────────────────────────────────────────────

    [Fact(DisplayName = "Deve retornar registros do usuário")]
    public async Task Execute_WithRecords_ShouldReturnAll()
    {
        var records = new[]
        {
            BuildRecord(2026, 4),
            BuildRecord(2026, 3)
        };
        _purgeRepo.Setup(r => r.GetByUserAsync(UserId, default))
                  .ReturnsAsync(records);

        var result = await _sut.ExecuteAsync(UserId);

        result.Should().HaveCount(2);
    }

    [Fact(DisplayName = "Deve retornar lista vazia se não houver registros")]
    public async Task Execute_WithNoRecords_ShouldReturnEmpty()
    {
        _purgeRepo.Setup(r => r.GetByUserAsync(UserId, default))
                  .ReturnsAsync(Enumerable.Empty<PurgeRecord>());

        var result = await _sut.ExecuteAsync(UserId);

        result.Should().BeEmpty();
    }

    // ── Filtro por userId ─────────────────────────────────────────────────────

    [Fact(DisplayName = "Deve filtrar por userId — nunca retornar registros de outro usuário")]
    public async Task Execute_ShouldFilterByUserId()
    {
        var userId = Guid.NewGuid();
        _purgeRepo.Setup(r => r.GetByUserAsync(userId, default))
                  .ReturnsAsync(Enumerable.Empty<PurgeRecord>());

        await _sut.ExecuteAsync(userId);

        // Deve chamar GetByUserAsync com o userId correto
        _purgeRepo.Verify(r => r.GetByUserAsync(userId, default), Times.Once);
        _purgeRepo.Verify(r => r.GetByUserAsync(It.IsNotIn(userId), default), Times.Never);
    }

    // ── Ordenação decrescente por PurgedAt ────────────────────────────────────

    [Fact(DisplayName = "Deve retornar registros ordenados de forma decrescente por PurgedAt")]
    public async Task Execute_WithMultipleRecords_ShouldReturnOrderedByPurgedAtDescending()
    {
        // O repositório mock retorna na ordem que o use case deve garantir
        var records = new[]
        {
            BuildRecord(2026, 4),
            BuildRecord(2026, 3),
            BuildRecord(2026, 1)
        };

        _purgeRepo.Setup(r => r.GetByUserAsync(UserId, default))
                  .ReturnsAsync(records);

        var result = (await _sut.ExecuteAsync(UserId)).ToList();

        // O use case deve garantir ordenação decrescente por PurgedAt
        result.Should().BeInDescendingOrder(r => r.PurgedAt);
    }

    // ── CancellationToken propagado ───────────────────────────────────────────

    [Fact(DisplayName = "Deve propagar o CancellationToken ao repositório")]
    public async Task Execute_ShouldPropagateCancellationToken()
    {
        var cts = new CancellationTokenSource();
        var ct  = cts.Token;

        _purgeRepo.Setup(r => r.GetByUserAsync(UserId, ct))
                  .ReturnsAsync(Enumerable.Empty<PurgeRecord>());

        await _sut.ExecuteAsync(UserId, ct);

        _purgeRepo.Verify(r => r.GetByUserAsync(UserId, ct), Times.Once);
    }
}
