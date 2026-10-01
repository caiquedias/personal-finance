using PersonalFinance.Application.DTOs.Import;

namespace PersonalFinance.Application.Interfaces;

/// <summary>
/// Serviço responsável por converter um extrato bancário em PDF (opcionalmente
/// protegido por senha) em uma lista de <see cref="ParsedStatementEntryDto"/>.
/// Implementado na camada Infrastructure.
/// </summary>
public interface IStatementParserService
{
    /// <summary>
    /// Lê o stream do PDF e retorna os lançamentos da tabela do extrato.
    /// Lança DomainException para senha incorreta/ausente ou stream que não é PDF.
    /// </summary>
    Task<IReadOnlyList<ParsedStatementEntryDto>> ParseAsync(
        Stream pdfStream,
        string? password,
        CancellationToken ct = default);
}
