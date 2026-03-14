using Microsoft.AspNetCore.Mvc;
using PersonalFinance.Application.DTOs.Config;
using PersonalFinance.Application.UseCases.Config;

namespace PersonalFinance.Api.Controllers.V1;

/// <summary>
/// CRUD completo de categorias de despesa.
/// </summary>
[Route("api/v1/categories")]
public sealed class CategoriesController(
    GetCategoriesUseCase getListUseCase,
    GetCategoryByIdUseCase getByIdUseCase,
    CreateCategoryUseCase createUseCase,
    UpdateCategoryUseCase updateUseCase,
    DeleteCategoryUseCase deleteUseCase) : ApiControllerBase
{
    private readonly GetCategoriesUseCase _getListUseCase = getListUseCase;
    private readonly GetCategoryByIdUseCase _getByIdUseCase = getByIdUseCase;
    private readonly CreateCategoryUseCase _createUseCase = createUseCase;
    private readonly UpdateCategoryUseCase _updateUseCase = updateUseCase;
    private readonly DeleteCategoryUseCase _deleteUseCase = deleteUseCase;

    /// <summary>Lista todas as categorias acessíveis ao usuário (próprias + globais).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<CategoryResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var result = await _getListUseCase.ExecuteAsync(CurrentUserId, ct);
        return Ok(result);
    }

    /// <summary>Retorna uma categoria específica acessível ao usuário.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(CategoryResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _getByIdUseCase.ExecuteAsync(id, CurrentUserId, ct);
        return Ok(result);
    }

    /// <summary>Cria uma nova categoria para o usuário autenticado.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(CategoryResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateCategoryDto dto, CancellationToken ct)
    {
        var enrichedDto = dto with { UserId = dto.IsGlobal ? null : CurrentUserId };
        var result      = await _createUseCase.ExecuteAsync(enrichedDto, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>Atualiza nome, cor e ícone de uma categoria do usuário.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCategoryDto dto, CancellationToken ct)
    {
        await _updateUseCase.ExecuteAsync(dto with { Id = id, UserId = CurrentUserId }, ct);
        return NoContent();
    }

    /// <summary>Exclui logicamente uma categoria. Bloqueado se houver despesas vinculadas.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _deleteUseCase.ExecuteAsync(id, CurrentUserId, ct);
        return NoContent();
    }
}
