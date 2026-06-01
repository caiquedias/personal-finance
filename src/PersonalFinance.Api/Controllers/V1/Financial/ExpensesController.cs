using Microsoft.AspNetCore.Mvc;
using PersonalFinance.Application.DTOs;
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
    CreateExpensesBatchUseCase createBatchUseCase,
    UpdateExpenseUseCase updateUseCase,
    DeleteExpenseUseCase deleteUseCase,
    DeleteExpensesBatchUseCase deleteBatchUseCase,
    PayExpensesBatchUseCase payBatchUseCase,
    CancelExpensesBatchUseCase cancelBatchUseCase,
    SaveExpenseOrderUseCase saveOrderUseCase,
    IExpenseRepository expenseRepository,
    IUnitOfWork unitOfWork) : ApiControllerBase
{
    private readonly GetExpensesByPeriodUseCase _getByPeriodUseCase = getByPeriodUseCase;
    private readonly GetExpenseByIdUseCase _getByIdUseCase = getByIdUseCase;
    private readonly CreateExpenseUseCase _createUseCase = createUseCase;
    private readonly CreateExpensesBatchUseCase _createBatchUseCase = createBatchUseCase;
    private readonly UpdateExpenseUseCase _updateUseCase = updateUseCase;
    private readonly DeleteExpenseUseCase _deleteUseCase = deleteUseCase;
    private readonly DeleteExpensesBatchUseCase _deleteBatchUseCase = deleteBatchUseCase;
    private readonly PayExpensesBatchUseCase _payBatchUseCase = payBatchUseCase;
    private readonly CancelExpensesBatchUseCase _cancelBatchUseCase = cancelBatchUseCase;
    private readonly SaveExpenseOrderUseCase _saveOrderUseCase = saveOrderUseCase;
    private readonly IExpenseRepository _expenseRepository = expenseRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    /// <summary>
    /// Lista despesas de um período com paginação e filtros opcionais.
    /// Requer query param: ?periodId={guid}
    /// Filtros opcionais: pageNumber, pageSize, description, categoryId, paymentStatus, fortnightType
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ExpenseResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetByPeriod(
        [FromQuery] Guid periodId,
        [FromQuery] ExpenseFilterDto filter,
        CancellationToken ct)
    {
        var result = await _getByPeriodUseCase.ExecuteAsync(periodId, CurrentUserId, filter, ct);
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

    /// <summary>Atualiza dados editáveis da despesa, incluindo status de pagamento.</summary>
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

    /// <summary>Cria múltiplas despesas em lote. Em caso de erro, nenhuma despesa é persistida.</summary>
    [HttpPost("batch/create")]
    [ProducesResponseType(typeof(IReadOnlyList<ExpenseResponseDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateBatch(
        [FromBody] CreateExpensesBatchDto dto, CancellationToken ct)
    {
        var result = await _createBatchUseCase.ExecuteAsync(
            dto with { UserId = CurrentUserId }, ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>Exclui logicamente múltiplas despesas em lote.</summary>
    [HttpDelete("batch")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeleteBatch(
        [FromBody] BatchExpenseActionDto dto, CancellationToken ct)
    {
        await _deleteBatchUseCase.ExecuteAsync(dto, CurrentUserId, ct);
        return NoContent();
    }

    /// <summary>Marca múltiplas despesas como pagas em lote. Data de pagamento = hoje (UTC).</summary>
    [HttpPatch("batch/pay")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PayBatch(
        [FromBody] BatchExpenseActionDto dto, CancellationToken ct)
    {
        await _payBatchUseCase.ExecuteAsync(dto, CurrentUserId, ct);
        return NoContent();
    }

    /// <summary>Marca múltiplas despesas como canceladas em lote.</summary>
    [HttpPatch("batch/cancel")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CancelBatch(
        [FromBody] BatchExpenseActionDto dto, CancellationToken ct)
    {
        await _cancelBatchUseCase.ExecuteAsync(dto, CurrentUserId, ct);
        return NoContent();
    }

    /// <summary>Persiste a ordenação de despesas definida via drag and drop.</summary>
    [HttpPost("order")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SaveOrder(
        [FromBody] IEnumerable<ExpenseOrderItemDto> items, CancellationToken ct)
    {
        var dto = new SaveExpenseOrderDto(CurrentUserId, items);
        await _saveOrderUseCase.ExecuteAsync(dto, ct);
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
