using PersonalFinance.Application.Options;
using PersonalFinance.Application.UseCases.Admin;

namespace PersonalFinance.Api.BackgroundServices;

/// <summary>
/// Executa o purge de audit log vencido no startup e a cada PurgeIntervalMinutes.
/// Falhas são logadas e não derrubam a aplicação.
/// </summary>
public sealed class AuditLogPurgeHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AuditLogRetentionOptions _options;
    private readonly ILogger<AuditLogPurgeHostedService> _logger;

    public AuditLogPurgeHostedService(
        IServiceScopeFactory scopeFactory,
        AuditLogRetentionOptions options,
        ILogger<AuditLogPurgeHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
            return;

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(_options.PurgeIntervalMinutes));
        try
        {
            do
            {
                await RunPurgeAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // Encerramento normal da aplicação
        }
    }

    private async Task RunPurgeAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var useCase = scope.ServiceProvider.GetRequiredService<PurgeExpiredAuditLogsUseCase>();
            var removed = await useCase.ExecuteAsync(ct);
            if (removed > 0)
                _logger.LogInformation("Audit log purge removed {Count} expired records.", removed);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Audit log purge failed; will retry at next interval.");
        }
    }
}
