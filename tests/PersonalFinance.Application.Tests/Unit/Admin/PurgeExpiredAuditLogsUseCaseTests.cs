using FluentAssertions;
using Moq;
using PersonalFinance.Application.Options;
using PersonalFinance.Application.UseCases.Admin;
using PersonalFinance.Domain.Interfaces.Repositories;
using Xunit;

namespace PersonalFinance.Application.Tests.Unit.Admin;

/// <summary>Purge por retenção do audit log, em lotes — #402.</summary>
public class PurgeExpiredAuditLogsUseCaseTests
{
    private readonly Mock<IAuditLogRepository> _repo = new();
    private readonly Mock<IUnitOfWork> _uow = new();

    private PurgeExpiredAuditLogsUseCase Sut(AuditLogRetentionOptions o) =>
        new(_repo.Object, _uow.Object, o);

    private static AuditLogRetentionOptions Opts(bool enabled = true, int days = 365, int batch = 100) =>
        new() { Enabled = enabled, RetentionDays = days, PurgeIntervalMinutes = 60, BatchSize = batch };

    [Fact(DisplayName = "Defaults de AuditLogRetentionOptions")]
    public void Options_Defaults()
    {
        var o = new AuditLogRetentionOptions();

        o.Enabled.Should().BeTrue();
        o.RetentionDays.Should().Be(365);
        o.PurgeIntervalMinutes.Should().BeGreaterThan(0);
        o.BatchSize.Should().BeGreaterThan(0);
    }

    [Fact(DisplayName = "Deve usar corte = agora - RetentionDays e BatchSize configurado")]
    public async Task Execute_ShouldUseCutoffFromRetentionDays()
    {
        var before = DateTime.UtcNow;
        _repo.Setup(r => r.RemoveOlderThanAsync(It.IsAny<DateTime>(), 100, It.IsAny<CancellationToken>())).ReturnsAsync(0);

        await Sut(Opts(days: 30)).ExecuteAsync();

        var after = DateTime.UtcNow;
        _repo.Verify(r => r.RemoveOlderThanAsync(
            It.Is<DateTime>(c => c >= before.AddDays(-30).AddSeconds(-1) && c <= after.AddDays(-30).AddSeconds(1)),
            100, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "Lote menor que BatchSize encerra: 1 chamada, 1 commit, retorna o total")]
    public async Task Execute_PartialBatch_ShouldStop()
    {
        _repo.Setup(r => r.RemoveOlderThanAsync(It.IsAny<DateTime>(), 100, It.IsAny<CancellationToken>())).ReturnsAsync(40);

        var removed = await Sut(Opts()).ExecuteAsync();

        removed.Should().Be(40);
        _repo.Verify(r => r.RemoveOlderThanAsync(It.IsAny<DateTime>(), 100, It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "Lotes cheios continuam até um lote parcial, com commit por lote")]
    public async Task Execute_FullBatches_ShouldLoopUntilPartial()
    {
        _repo.SetupSequence(r => r.RemoveOlderThanAsync(It.IsAny<DateTime>(), 100, It.IsAny<CancellationToken>()))
             .ReturnsAsync(100).ReturnsAsync(100).ReturnsAsync(30);

        var removed = await Sut(Opts()).ExecuteAsync();

        removed.Should().Be(230);
        _repo.Verify(r => r.RemoveOlderThanAsync(It.IsAny<DateTime>(), 100, It.IsAny<CancellationToken>()), Times.Exactly(3));
        _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [Fact(DisplayName = "Nada a remover: retorna 0 e não faz commit")]
    public async Task Execute_NothingToRemove_ShouldNotCommit()
    {
        _repo.Setup(r => r.RemoveOlderThanAsync(It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(0);

        var removed = await Sut(Opts()).ExecuteAsync();

        removed.Should().Be(0);
        _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "Enabled=false: não toca no repositório nem faz commit e retorna 0")]
    public async Task Execute_Disabled_ShouldDoNothing()
    {
        var removed = await Sut(Opts(enabled: false)).ExecuteAsync();

        removed.Should().Be(0);
        _repo.Invocations.Should().BeEmpty();
        _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "CancellationToken cancelado deve interromper com OperationCanceledException")]
    public async Task Execute_Cancelled_ShouldThrow()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        _repo.Setup(r => r.RemoveOlderThanAsync(It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(100);

        var act = () => Sut(Opts()).ExecuteAsync(cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
