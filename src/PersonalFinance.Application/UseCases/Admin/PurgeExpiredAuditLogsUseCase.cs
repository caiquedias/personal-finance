using PersonalFinance.Application.Options;
using PersonalFinance.Domain.Interfaces.Repositories;

namespace PersonalFinance.Application.UseCases.Admin;

/// <summary>Remove, em lotes, registros de auditoria além do período de retenção.</summary>
public sealed class PurgeExpiredAuditLogsUseCase
{
    private readonly IAuditLogRepository _repository;
    private readonly IUnitOfWork _uow;
    private readonly AuditLogRetentionOptions _options;

    public PurgeExpiredAuditLogsUseCase(
        IAuditLogRepository repository, IUnitOfWork uow, AuditLogRetentionOptions options)
    {
        _repository = repository;
        _uow = uow;
        _options = options;
    }

    /// <summary>Retorna o total de registros removidos.</summary>
    public async Task<int> ExecuteAsync(CancellationToken ct = default)
    {
        if (!_options.Enabled)
            return 0;

        var cutoff = DateTime.UtcNow.AddDays(-_options.RetentionDays);
        var total = 0;

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            var removed = await _repository.RemoveOlderThanAsync(cutoff, _options.BatchSize, ct);
            if (removed == 0)
                break;

            await _uow.CommitAsync(ct);
            total += removed;

            // Lote parcial = não há mais registros expirados
            if (removed < _options.BatchSize)
                break;
        }

        return total;
    }
}
