// Stub de compilação — permite que CsvExportServiceTests compile enquanto
// a implementação real (CsvExportService) ainda não existe no projeto Infrastructure.
// Será removido quando o Green criar o arquivo de produção real.

using PersonalFinance.Application.Interfaces;
using PersonalFinance.Infrastructure.Persistence.Context;

namespace PersonalFinance.Infrastructure.Services;

/// <summary>
/// Stub de compilação do CsvExportService.
/// Lança NotImplementedException em todos os métodos — o Green implementará a versão real.
/// </summary>
internal sealed class CsvExportService : ICsvExportService
{
    public CsvExportService(AppDbContext context)
    {
        // Stub — sem implementação
    }

    public Task<string> ExportPeriodAsync(
        Guid periodId,
        Guid userId,
        CancellationToken ct = default)
        => throw new NotImplementedException(
            "CsvExportService não implementado — aguardando fase Green.");
}
