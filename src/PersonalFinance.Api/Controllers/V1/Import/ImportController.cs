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

    public ImportController(ImportLegacyDataUseCase importUseCase)
    {
        _importUseCase = importUseCase;
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
}
