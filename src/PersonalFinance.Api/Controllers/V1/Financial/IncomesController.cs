using Microsoft.AspNetCore.Mvc;
using PersonalFinance.Application.DTOs;
using PersonalFinance.Application.DTOs.Financial;
using PersonalFinance.Application.UseCases.Financial.Incomes;

namespace PersonalFinance.Api.Controllers.V1;

/// <summary>
/// CRUD completo de receitas mensais.
/// </summary>
[Route("api/v1/incomes")]
public sealed class IncomesController : ApiControllerBase
{
    private readonly GetIncomesByPeriodUseCase _getByPeriodUseCase;
    private readonly GetIncomeByIdUseCase      _getByIdUseCase;
    private readonly CreateIncomeUseCase       _createUseCase;
    private readonly UpdateIncomeUseCase       _updateUseCase;
    private readonly DeleteIncomeUseCase       _deleteUseCase;

    public IncomesController(
        GetIncomesByPeriodUseCase getByPeriodUseCase,
        GetIncomeByIdUseCase      getByIdUseCase,
        CreateIncomeUseCase       createUseCase,
        UpdateIncomeUseCase       updateUseCase,
        DeleteIncomeUseCase       deleteUseCase)
    {
        _getByPeriodUseCase = getByPeriodUseCase;
        _getByIdUseCase     = getByIdUseCase;
        _createUseCase      = createUseCase;
        _updateUseCase      = updateUseCase;
        _deleteUseCase      = deleteUseCase;
    }

    /// <summary>
    /// Lista todas as receitas de um período.
    /// Requer query param: ?periodId={guid}
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<IncomeResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetByPeriod(
        [FromQuery] Guid periodId,
        [FromQuery] IncomeFilterDto filter,
        CancellationToken ct)
    {
        var result = await _getByPeriodUseCase.ExecuteAsync(periodId, CurrentUserId, filter, ct);
        return Ok(result);
    }

    /// <summary>Retorna uma receita específica do usuário.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(IncomeResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _getByIdUseCase.ExecuteAsync(id, CurrentUserId, ct);
        return Ok(result);
    }

    /// <summary>Registra uma nova receita em um período existente.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(IncomeResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateIncomeDto dto, CancellationToken ct)
    {
        var result = await _createUseCase.ExecuteAsync(dto with { UserId = CurrentUserId }, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>Atualiza os dados editáveis de uma receita existente.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateIncomeDto dto, CancellationToken ct)
    {
        await _updateUseCase.ExecuteAsync(dto with { Id = id, UserId = CurrentUserId }, ct);
        return NoContent();
    }

    /// <summary>Exclui logicamente uma receita.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _deleteUseCase.ExecuteAsync(id, CurrentUserId, ct);
        return NoContent();
    }
}
