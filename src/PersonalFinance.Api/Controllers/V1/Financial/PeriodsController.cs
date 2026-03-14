using Microsoft.AspNetCore.Mvc;
using PersonalFinance.Application.DTOs.Financial;
using PersonalFinance.Application.UseCases.Financial.Periods;

namespace PersonalFinance.Api.Controllers.V1;

/// <summary>
/// Gerenciamento completo de períodos mensais e resumo financeiro.
/// </summary>
[Route("api/v1/periods")]
public sealed class PeriodsController : ApiControllerBase
{
    private readonly GetPeriodsByUserUseCase _getListUseCase;
    private readonly GetPeriodByIdUseCase    _getByIdUseCase;
    private readonly CreatePeriodUseCase     _createUseCase;
    private readonly GetPeriodSummaryUseCase _summaryUseCase;

    public PeriodsController(
        GetPeriodsByUserUseCase getListUseCase,
        GetPeriodByIdUseCase    getByIdUseCase,
        CreatePeriodUseCase     createUseCase,
        GetPeriodSummaryUseCase summaryUseCase)
    {
        _getListUseCase = getListUseCase;
        _getByIdUseCase = getByIdUseCase;
        _createUseCase  = createUseCase;
        _summaryUseCase = summaryUseCase;
    }

    /// <summary>Lista todos os períodos do usuário, ordenados por ano/mês descendente.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<PeriodResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var result = await _getListUseCase.ExecuteAsync(CurrentUserId, ct);
        return Ok(result);
    }

    /// <summary>Retorna um período específico do usuário.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PeriodResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _getByIdUseCase.ExecuteAsync(id, CurrentUserId, ct);
        return Ok(result);
    }

    /// <summary>
    /// Cria um novo período mensal. Retorna 400 se já existir para o mês/ano.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(PeriodResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreatePeriodDto dto, CancellationToken ct)
    {
        var result = await _createUseCase.ExecuteAsync(dto with { UserId = CurrentUserId }, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>
    /// Retorna o resumo financeiro do período (vw_PeriodSummary): receitas, despesas,
    /// pago, devedor, totais por quinzena e saldo.
    /// </summary>
    [HttpGet("{id:guid}/summary")]
    [ProducesResponseType(typeof(PeriodSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetSummary(Guid id, CancellationToken ct)
    {
        var result = await _summaryUseCase.ExecuteAsync(id, CurrentUserId, ct);
        return Ok(result);
    }
}
