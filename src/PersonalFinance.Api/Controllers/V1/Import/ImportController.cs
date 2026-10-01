using Microsoft.AspNetCore.Mvc;
using PersonalFinance.Application.DTOs.Import;
using PersonalFinance.Application.UseCases.Import;

namespace PersonalFinance.Api.Controllers.V1.Import;

/// <summary>
/// Endpoint de importação do histórico financeiro legado (planilha Excel).
/// Restrito a usuários autenticados — cada usuário importa para sua própria conta.
/// </summary>
[Route("api/v1/import")]
public sealed class ImportController : ApiControllerBase
{
    private readonly ImportLegacyDataUseCase _importUseCase;
    private readonly PreviewStatementImportUseCase _previewUseCase;
    private readonly ConfirmStatementImportUseCase _confirmUseCase;

    public ImportController(
        ImportLegacyDataUseCase importUseCase,
        PreviewStatementImportUseCase previewUseCase,
        ConfirmStatementImportUseCase confirmUseCase)
    {
        _importUseCase = importUseCase;
        _previewUseCase = previewUseCase;
        _confirmUseCase = confirmUseCase;
    }

    /// <summary>
    /// Importa o histórico financeiro de uma planilha .xlsx no formato legado.
    ///
    /// Regras:
    /// - Aceita apenas arquivos .xlsx
    /// - Períodos já existentes são ignorados (operação idempotente)
    /// - Retorna um sumário com o número de registros importados e avisos
    ///
    /// Content-Type: multipart/form-data
    /// Campo: file (IFormFile)
    /// </summary>
    /// <response code="200">Importação concluída — retorna sumário com contagens e avisos.</response>
    /// <response code="400">Arquivo inválido (não é .xlsx, vazio ou corrompido).</response>
    [HttpPost("legacy")]
    [RequestSizeLimit(10 * 1024 * 1024)] // 10 MB
    [ProducesResponseType(typeof(ImportResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ImportLegacy(
        IFormFile file,
        CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "Nenhum arquivo enviado." });

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext != ".xlsx")
            return BadRequest(new { message = "Apenas arquivos .xlsx são aceitos." });

        await using var stream = file.OpenReadStream();
        var result = await _importUseCase.ExecuteAsync(stream, CurrentUserId, ct);

        return Ok(result);
    }

    /// <summary>
    /// Gera o preview de importação de um extrato PDF (não persiste nada).
    /// Classifica Receita/Despesa, sugere categoria e sinaliza transferências internas e duplicatas.
    ///
    /// Content-Type: multipart/form-data
    /// Campos: file (PDF), password (opcional), fromDate (opcional, yyyy-MM-dd)
    /// </summary>
    /// <response code="200">Preview gerado.</response>
    /// <response code="400">Arquivo ausente, não é .pdf, senha incorreta ou PDF inválido.</response>
    [HttpPost("statement/preview")]
    [RequestSizeLimit(10 * 1024 * 1024)] // 10 MB
    [ProducesResponseType(typeof(StatementPreviewResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PreviewStatement(
        IFormFile file,
        [FromForm] string? password,
        [FromForm] DateOnly? fromDate,
        CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "Nenhum arquivo enviado." });

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext != ".pdf")
            return BadRequest(new { message = "Apenas arquivos .pdf são aceitos." });

        await using var stream = file.OpenReadStream();
        var result = await _previewUseCase.ExecuteAsync(
            stream, password, fromDate ?? DateOnly.MinValue, CurrentUserId, ct);

        return Ok(result);
    }

    /// <summary>
    /// Confirma a importação do extrato: persiste os lançamentos selecionados
    /// (Receitas/Despesas) nos períodos correspondentes, criando-os quando necessário.
    /// </summary>
    /// <response code="200">Importação confirmada — retorna sumário.</response>
    /// <response code="400">Payload inválido ou regra de negócio violada.</response>
    [HttpPost("statement/confirm")]
    [ProducesResponseType(typeof(ConfirmStatementImportResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ConfirmStatement(
        [FromBody] ConfirmStatementImportRequestDto request,
        CancellationToken ct)
    {
        var result = await _confirmUseCase.ExecuteAsync(request, CurrentUserId, ct);
        return Ok(result);
    }
}
