using PersonalFinance.Application.Interfaces;
using PersonalFinance.Domain.Exceptions;
using PersonalFinance.Domain.Interfaces.Repositories;

namespace PersonalFinance.Application.UseCases.Financial.Purge;

/// <summary>
/// Exporta os dados de um período para CSV, validando o ownership antes de delegar ao serviço.
/// </summary>
public sealed class ExportPeriodUseCase
{
    private readonly IPeriodRepository  _periodRepository;
    private readonly ICsvExportService  _csvExportService;

    public ExportPeriodUseCase(
        IPeriodRepository periodRepository,
        ICsvExportService csvExportService)
    {
        _periodRepository = periodRepository;
        _csvExportService = csvExportService;
    }

    public async Task<string> ExecuteAsync(
        Guid periodId,
        Guid userId,
        CancellationToken ct = default)
    {
        _ = await _periodRepository.GetByIdAndUserAsync(periodId, userId, ct)
            ?? throw new DomainException(
                "Período não encontrado ou sem permissão de acesso.");

        return await _csvExportService.ExportPeriodAsync(periodId, userId, ct);
    }
}
