using Microsoft.AspNetCore.Mvc;
using PersonalFinance.Application.DTOs.Financial;
using PersonalFinance.Application.UseCases.Financial.Expenses;
using PersonalFinance.Domain.Interfaces.Repositories;

namespace PersonalFinance.Api.Controllers.V1;

/// <summary>
/// CRUD completo de despesas + ações de status de pagamento.
/// </summary>
[Route("api/v1/expenses")]
public sealed class ExpensesController(
    GetExpensesByPeriodUseCase getByPeriodUseCase,
    GetExpenseByIdUseCase getByIdUseCase,
    CreateExpenseUseCase createUseCase,
    UpdateExpenseUseCase updateUseCase,
    DeleteExpenseUseCase deleteUseCase,
    IExpenseRepository expenseRepository,
    IUnitOfWork unitOfWork) : ApiControllerBase
{
    private readonly GetExpensesByPeriodUseCase _getByPeriodUseCase = getByPeriodUseCase;
    private readonly GetExpenseByIdUseCase _getByIdUseCase = getByIdUseCase;
    private readonly CreateExpenseUseCase _createUseCase = createUseCase;
    private readonly UpdateExpenseUseCase _updateUseCase = updateUseCase;
    private readonly DeleteExpenseUseCase _deleteUseCase = deleteUseCase;
    private readonly IExpenseRepository _expenseRepository = expenseRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    /// <summary>
    /// Lista todas as despesas de um período.
    /// Requer query param: ?periodId={guid}
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ExpenseResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetByPeriod(
        [FromQuery] Guid periodId,
        CancellationToken ct)
    {
        var result = await _getByPeriodUseCase.ExecuteAsync(periodId, CurrentUserId, ct);
        return Ok(result);
    }

    /// <summary>Retorna uma despesa específica do usuário.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ExpenseResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _getByIdUseCase.ExecuteAsync(id, CurrentUserId, ct);
        return Ok(result);
    }

    /// <summary>Cria uma nova despesa. Status inicial sempre Pending.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ExpenseResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateExpenseDto dto, CancellationToken ct)
    {
        var result = await _createUseCase.ExecuteAsync(dto with { UserId = CurrentUserId }, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>Atualiza dados editáveis da despesa. Não altera status — use PATCH.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateExpenseDto dto, CancellationToken ct)
    {
        await _updateUseCase.ExecuteAsync(dto with { Id = id, UserId = CurrentUserId }, ct);
        return NoContent();
    }

    /// <summary>Marca a despesa como paga. Data de pagamento não pode ser futura.</summary>
    [HttpPatch("{id:guid}/pay")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkAsPaid(Guid id, [FromBody] MarkAsPaidDto dto, CancellationToken ct)
    {
        var expense = await _expenseRepository.GetByIdAndUserAsync(id, CurrentUserId, ct)
            ?? throw new KeyNotFoundException("Despesa não encontrada.");

        expense.MarkAsPaid(dto.PaymentDate);
        await _expenseRepository.UpdateAsync(expense, ct);
        await _unitOfWork.CommitAsync(ct);
        return NoContent();
    }

    /// <summary>Marca a despesa como cancelada.</summary>
    [HttpPatch("{id:guid}/cancel")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        var expense = await _expenseRepository.GetByIdAndUserAsync(id, CurrentUserId, ct)
            ?? throw new KeyNotFoundException("Despesa não encontrada.");

        expense.MarkAsCancelled();
        await _expenseRepository.UpdateAsync(expense, ct);
        await _unitOfWork.CommitAsync(ct);
        return NoContent();
    }

    /// <summary>Marca a despesa como parcialmente paga.</summary>
    [HttpPatch("{id:guid}/partial")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkAsPartial(Guid id, CancellationToken ct)
    {
        var expense = await _expenseRepository.GetByIdAndUserAsync(id, CurrentUserId, ct)
            ?? throw new KeyNotFoundException("Despesa não encontrada.");

        expense.MarkAsPartial();
        await _expenseRepository.UpdateAsync(expense, ct);
        await _unitOfWork.CommitAsync(ct);
        return NoContent();
    }

    /// <summary>Exclui logicamente uma despesa.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _deleteUseCase.ExecuteAsync(id, CurrentUserId, ct);
        return NoContent();
    }
}

/// <summary>DTO para o endpoint PATCH /expenses/{id}/pay</summary>
public sealed record MarkAsPaidDto(DateOnly PaymentDate);
