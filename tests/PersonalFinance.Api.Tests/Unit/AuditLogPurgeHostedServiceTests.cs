using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using PersonalFinance.Api.BackgroundServices;
using PersonalFinance.Application.Options;
using PersonalFinance.Application.UseCases.Admin;
using PersonalFinance.Domain.Interfaces.Repositories;
using Xunit;

namespace PersonalFinance.Api.Tests.Unit;

/// <summary>
/// AuditLogPurgeHostedService (#402): roda o purge no startup, não derruba a app em caso de falha
/// (loga e segue) e não executa com Enabled=false.
/// </summary>
public class AuditLogPurgeHostedServiceTests
{
    private readonly Mock<IAuditLogRepository> _repo = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<ILogger<AuditLogPurgeHostedService>> _logger = new();

    private AuditLogPurgeHostedService Build(AuditLogRetentionOptions options)
    {
        var services = new ServiceCollection();
        services.AddSingleton(_repo.Object);
        services.AddSingleton(_uow.Object);
        services.AddSingleton(options);
        services.AddScoped<PurgeExpiredAuditLogsUseCase>();
        var provider = services.BuildServiceProvider();
        return new AuditLogPurgeHostedService(
            provider.GetRequiredService<IServiceScopeFactory>(), options, _logger.Object);
    }

    private static AuditLogRetentionOptions Opts(bool enabled = true) =>
        new() { Enabled = enabled, RetentionDays = 365, PurgeIntervalMinutes = 60, BatchSize = 100 };

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition() && DateTime.UtcNow < deadline)
            await Task.Delay(20);
    }

    [Fact(DisplayName = "Enabled=true deve executar o purge no startup")]
    public async Task Start_Enabled_ShouldRunPurgeAtStartup()
    {
        _repo.Setup(r => r.RemoveOlderThanAsync(It.IsAny<DateTime>(), 100, It.IsAny<CancellationToken>())).ReturnsAsync(0);
        var sut = Build(Opts());

        await sut.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => _repo.Invocations.Count > 0);
        await sut.StopAsync(CancellationToken.None);

        _repo.Verify(r => r.RemoveOlderThanAsync(It.IsAny<DateTime>(), 100, It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact(DisplayName = "Enabled=false não deve executar o purge")]
    public async Task Start_Disabled_ShouldNotRunPurge()
    {
        var sut = Build(Opts(enabled: false));

        await sut.StartAsync(CancellationToken.None);
        await Task.Delay(300);
        await sut.StopAsync(CancellationToken.None);

        _repo.Invocations.Should().BeEmpty();
    }

    [Fact(DisplayName = "Exceção no purge deve ser logada e não derrubar o serviço")]
    public async Task Start_WhenPurgeThrows_ShouldLogAndKeepRunning()
    {
        _repo.Setup(r => r.RemoveOlderThanAsync(It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
             .ThrowsAsync(new InvalidOperationException("falha simulada"));
        var sut = Build(Opts());

        await sut.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => _logger.Invocations.Any(i => i.Method.Name == nameof(ILogger.Log)));

        _logger.Verify(l => l.Log(
            It.Is<LogLevel>(lv => lv >= LogLevel.Warning),
            It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(),
            It.IsAny<Exception?>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.AtLeastOnce);
        sut.ExecuteTask.Should().NotBeNull();
        sut.ExecuteTask!.IsFaulted.Should().BeFalse();
        sut.ExecuteTask.IsCompleted.Should().BeFalse("o loop continua aguardando o próximo intervalo");

        var stop = () => sut.StopAsync(CancellationToken.None);
        await stop.Should().NotThrowAsync();
    }

    [Fact(DisplayName = "StopAsync deve encerrar o loop")]
    public async Task Stop_ShouldCompleteExecuteTask()
    {
        _repo.Setup(r => r.RemoveOlderThanAsync(It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(0);
        var sut = Build(Opts());

        await sut.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => _repo.Invocations.Count > 0);
        await sut.StopAsync(CancellationToken.None);

        sut.ExecuteTask!.IsCompleted.Should().BeTrue();
    }
}
