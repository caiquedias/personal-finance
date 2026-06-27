using Microsoft.AspNetCore.Mvc;
using PersonalFinance.Application.UseCases.Financial.Purge;
using PersonalFinance.Domain.Interfaces.Repositories;

namespace PersonalFinance.Api.Controllers.V1.Purge;

/// <summary>
/// Endpoints de expurgo: exportação CSV, execução de expurgo e gerenciamento de registros.
/// </summary>
[Route("api/v1/purge")]
public sealed class PurgeController(
    ExportPeriodUseCase exportPeriodUseCase,
    PurgePeriodUseCase purgePeriodUseCase,
    GetPurgeRecordsUseCase getPurgeRecordsUseCase,
    DeletePurgeRecordUseCase deletePurgeRecordUseCase,
    IPeriodRepository periodRepository,
    IExpenseRepository expenseRepository,
    IIncomeRepository incomeRepository) : ApiControllerBase
{
    private readonly ExportPeriodUseCase     _exportPeriodUseCase     = exportPeriodUseCase;
    private readonly PurgePeriodUseCase      _purgePeriodUseCase      = purgePeriodUseCase;
    private readonly GetPurgeRecordsUseCase  _getPurgeRecordsUseCase  = getPurgeRecordsUseCase;
    private readonly DeletePurgeRecordUseCase _deletePurgeRecordUseCase = deletePurgeRecordUseCase;
    private readonly IPeriodRepository       _periodRepository        = periodRepository;
    private readonly IExpenseRepository      _expenseRepository       = expenseRepository;
    private readonly IIncomeRepository       _incomeRepository        = incomeRepository;

    /// <summary>
    /// Lista os períodos elegíveis ao expurgo (IsActive = false) do usuário autenticado.
    /// Retorna totais de receitas, despesas e contagem de itens por período.
    /// </summary>
    [HttpGet("eligible-periods")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEligiblePeriods(CancellationToken ct)
    {
        var periods = await _periodRepository.GetByUserAsync(CurrentUserId, ct);
        var eligiblePeriods = periods.Where(p => !p.IsActive).ToList();

        var result = new List<object>(eligiblePeriods.Count);
        foreach (var p in eligiblePeriods)
        {
            var expenses = (await _expenseRepository.GetByPeriodAsync(p.Id, CurrentUserId, ct)).ToList();
            var incomes  = (await _incomeRepository.GetByPeriodAsync(p.Id, CurrentUserId, ct)).ToList();

            result.Add(new
            {
                periodId     = p.Id,
                year         = p.Year,
                month        = p.Month,
                totalIncome  = incomes.Sum(i => i.Amount),
                totalExpense = expenses.Sum(e => e.Amount),
                itemCount    = expenses.Count + incomes.Count
            });
        }

        return Ok(result);
    }

    /// <summary>
    /// Exporta os dados de um período inativo como CSV.
    /// Retorna o arquivo com Content-Type text/csv e Content-Disposition para download.
    /// </summary>
    [HttpGet("{periodId:guid}/export")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ExportPeriod(Guid periodId, CancellationToken ct)
    {
        var period = await _periodRepository.GetByIdAndUserAsync(periodId, CurrentUserId, ct)
            ?? throw new KeyNotFoundException("Período não encontrado ou sem permissão de acesso.");

        var csv = await _exportPeriodUseCase.ExecuteAsync(periodId, CurrentUserId, ct);
        var fileName = $"pf_{period.Year}_{period.Month:D2}_expurgo.csv";

        var bytes = System.Text.Encoding.UTF8.GetBytes(csv);
        return File(bytes, "text/csv", fileName);
    }

    /// <summary>
    /// Executa o expurgo de um período inativo.
    /// Cria um PurgeRecord com snapshot dos dados e deleta fisicamente o período.
    /// </summary>
    [HttpPost("{periodId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Purge(
        Guid periodId,
        [FromBody] PurgeRequestDto dto,
        CancellationToken ct)
    {
        _ = await _periodRepository.GetByIdAndUserAsync(periodId, CurrentUserId, ct)
            ?? throw new KeyNotFoundException("Período não encontrado ou sem permissão de acesso.");

        await _purgePeriodUseCase.ExecuteAsync(periodId, CurrentUserId, dto.CsvFileName, ct);
        return NoContent();
    }

    /// <summary>Lista todos os registros de expurgo do usuário autenticado.</summary>
    [HttpGet("records")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRecords(CancellationToken ct)
    {
        var records = await _getPurgeRecordsUseCase.ExecuteAsync(CurrentUserId, ct);
        return Ok(records);
    }

    /// <summary>Remove um registro de expurgo do usuário autenticado.</summary>
    [HttpDelete("records/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteRecord(Guid id, CancellationToken ct)
    {
        // Valida ownership — lança 404 se não encontrado
        var records = await _getPurgeRecordsUseCase.ExecuteAsync(CurrentUserId, ct);
        if (!records.Any(r => r.Id == id))
            throw new KeyNotFoundException("Registro de expurgo não encontrado ou sem permissão de acesso.");

        await _deletePurgeRecordUseCase.ExecuteAsync(id, CurrentUserId, ct);
        return NoContent();
    }
}
