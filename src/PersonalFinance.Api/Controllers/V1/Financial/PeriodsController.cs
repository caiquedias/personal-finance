using Microsoft.AspNetCore.Mvc;
using PersonalFinance.Application.DTOs.Financial;
using PersonalFinance.Application.UseCases.Financial.Periods;

namespace PersonalFinance.Api.Controllers.V1;

[Route("api/v1/periods")]
public sealed class PeriodsController : ApiControllerBase
{
    private readonly GetPeriodsByUserUseCase  _getListUseCase;
    private readonly GetPeriodByIdUseCase     _getByIdUseCase;
    private readonly CreatePeriodUseCase      _createUseCase;
    private readonly GetPeriodSummaryUseCase  _summaryUseCase;
    private readonly TogglePeriodActiveUseCase _toggleUseCase;
    private readonly DeletePeriodUseCase      _deleteUseCase;

    public PeriodsController(
        GetPeriodsByUserUseCase   getListUseCase,
        GetPeriodByIdUseCase      getByIdUseCase,
        CreatePeriodUseCase       createUseCase,
        GetPeriodSummaryUseCase   summaryUseCase,
        TogglePeriodActiveUseCase toggleUseCase,
        DeletePeriodUseCase       deleteUseCase)
    {
        _getListUseCase = getListUseCase;
        _getByIdUseCase = getByIdUseCase;
        _createUseCase  = createUseCase;
        _summaryUseCase = summaryUseCase;
        _toggleUseCase  = toggleUseCase;
        _deleteUseCase  = deleteUseCase;
    }

    /// <summary>Lista todos os períodos do usuário ordenados por ano/mês descendente.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<PeriodResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
        => Ok(await _getListUseCase.ExecuteAsync(CurrentUserId, ct));

    /// <summary>Retorna um período específico do usuário.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PeriodResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => Ok(await _getByIdUseCase.ExecuteAsync(id, CurrentUserId, ct));

    /// <summary>Cria um novo período mensal. Retorna 400 se já existir para o mês/ano.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(PeriodResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreatePeriodDto dto, CancellationToken ct)
    {
        var result = await _createUseCase.ExecuteAsync(dto with { UserId = CurrentUserId }, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>
    /// Alterna o status ativo/inativo do período (toggle).
    /// Períodos inativos não aparecem nas listagens com filtro padrão.
    /// </summary>
    [HttpPatch("{id:guid}/toggle-active")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ToggleActive(Guid id, CancellationToken ct)
    {
        await _toggleUseCase.ExecuteAsync(id, CurrentUserId, ct);
        return NoContent();
    }

    /// <summary>
    /// Exclui logicamente um período.
    /// Bloqueado se o período possuir despesas ou receitas vinculadas.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _deleteUseCase.ExecuteAsync(id, CurrentUserId, ct);
        return NoContent();
    }

    /// <summary>Retorna o resumo financeiro do período via vw_PeriodSummary.</summary>
    [HttpGet("{id:guid}/summary")]
    [ProducesResponseType(typeof(PeriodSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetSummary(Guid id, CancellationToken ct)
        => Ok(await _summaryUseCase.ExecuteAsync(id, CurrentUserId, ct));
}
