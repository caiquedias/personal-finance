namespace PersonalFinance.Application.Interfaces;

public interface ICsvExportService
{
    Task<string> ExportPeriodAsync(Guid periodId, Guid userId, CancellationToken ct = default);
}
