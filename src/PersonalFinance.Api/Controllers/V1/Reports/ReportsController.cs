using Microsoft.AspNetCore.Mvc;
using PersonalFinance.Application.DTOs.Reports;
using PersonalFinance.Application.UseCases.Reports;

namespace PersonalFinance.Api.Controllers.V1.Reports;

/// <summary>
/// Endpoints de relatórios de despesas.
/// </summary>
[Route("api/v1/reports")]
public sealed class ReportsController(GetExpensesReportUseCase getExpensesReportUseCase)
    : ApiControllerBase
{
    private readonly GetExpensesReportUseCase _getExpensesReportUseCase = getExpensesReportUseCase;

    /// <summary>
    /// Retorna despesas agrupadas por categoria para o ano informado.
    /// Quando month é informado, filtra pelo mês específico.
    /// </summary>
    [HttpGet("expenses")]
    [ProducesResponseType(typeof(ExpensesReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetExpenses(
        [FromQuery] int year,
        [FromQuery] int? month,
        CancellationToken ct)
    {
        if (year <= 0)
            return BadRequest("O ano é obrigatório.");

        if (month.HasValue && (month < 1 || month > 12))
            return BadRequest("O mês deve estar entre 1 e 12.");

        var result = await _getExpensesReportUseCase.ExecuteAsync(CurrentUserId, year, month, ct);
        return Ok(result);
    }
}
