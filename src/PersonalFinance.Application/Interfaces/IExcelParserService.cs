using PersonalFinance.Application.DTOs.Import;

namespace PersonalFinance.Application.Interfaces;

/// <summary>
/// Serviço responsável por converter um arquivo .xlsx no formato da planilha
/// de gastos legada em uma lista de <see cref="ParsedSheetDto"/>.
/// Implementado na camada Infrastructure usando ClosedXML.
/// </summary>
public interface IExcelParserService
{
    /// <summary>
    /// Lê o stream do arquivo .xlsx e retorna uma lista de abas parseadas,
    /// uma por período mensal encontrado.
    /// </summary>
    Task<IReadOnlyList<ParsedSheetDto>> ParseAsync(
        Stream fileStream,
        CancellationToken ct = default);
}
