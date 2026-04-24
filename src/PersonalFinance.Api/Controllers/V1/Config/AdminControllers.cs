using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalFinance.Application.DTOs;
using PersonalFinance.Application.DTOs.Admin;
using PersonalFinance.Application.UseCases.Admin;
using PersonalFinance.Application.UseCases.Config;
using PersonalFinance.Domain.Exceptions;

namespace PersonalFinance.Api.Controllers.V1.Config;

// ══════════════════════════════════════════════════════════════════════════════
// CONFIG CONTROLLER — lookup tables gerenciáveis
// ══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Gerenciamento das tabelas de configuração (lookup).
/// GET: disponível para todos os usuários autenticados (necessário nos formulários).
/// POST: restrito a administradores.
/// Valores de seed (IsSystemSeed=true) não podem ser excluídos ou alterados.
/// </summary>
[Route("api/v1/config")]
public sealed class ConfigController(
    GetPaymentStatusesUseCase getPaymentStatuses,
    CreatePaymentStatusUseCase createPaymentStatus,
    UpdatePaymentStatusUseCase updatePaymentStatus,
    DeletePaymentStatusUseCase deletePaymentStatus,
    GetSourceTypesUseCase getSourceTypes,
    CreateSourceTypeUseCase createSourceType,
    UpdateSourceTypeUseCase updateSourceType,
    DeleteSourceTypeUseCase deleteSourceType,
    GetFortnightTypesUseCase getFortnightTypes,
    CreateFortnightTypeUseCase createFortnightType,
    UpdateFortnightTypeUseCase updateFortnightType,
    DeleteFortnightTypeUseCase deleteFortnightType) : ApiControllerBase
{
    private readonly GetPaymentStatusesUseCase    _getPaymentStatuses    = getPaymentStatuses;
    private readonly CreatePaymentStatusUseCase   _createPaymentStatus   = createPaymentStatus;
    private readonly UpdatePaymentStatusUseCase   _updatePaymentStatus   = updatePaymentStatus;
    private readonly DeletePaymentStatusUseCase   _deletePaymentStatus   = deletePaymentStatus;
    private readonly GetSourceTypesUseCase        _getSourceTypes        = getSourceTypes;
    private readonly CreateSourceTypeUseCase      _createSourceType      = createSourceType;
    private readonly UpdateSourceTypeUseCase      _updateSourceType      = updateSourceType;
    private readonly DeleteSourceTypeUseCase      _deleteSourceType      = deleteSourceType;
    private readonly GetFortnightTypesUseCase     _getFortnightTypes     = getFortnightTypes;
    private readonly CreateFortnightTypeUseCase   _createFortnightType   = createFortnightType;
    private readonly UpdateFortnightTypeUseCase   _updateFortnightType   = updateFortnightType;
    private readonly DeleteFortnightTypeUseCase   _deleteFortnightType   = deleteFortnightType;

    // ── Payment Status ────────────────────────────────────────────────────────

    /// <summary>Lista todos os status de pagamento (seed + personalizados).</summary>
    [HttpGet("payment-statuses")]
    [ProducesResponseType(typeof(IEnumerable<LookupResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPaymentStatuses(CancellationToken ct)
        => Ok(await _getPaymentStatuses.ExecuteAsync(ct));

    /// <summary>Cria um novo status de pagamento personalizado. Somente Admin.</summary>
    [HttpPost("payment-statuses")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(LookupResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreatePaymentStatus(
        [FromBody] CreatePaymentStatusDto dto, CancellationToken ct)
    {
        var result = await _createPaymentStatus.ExecuteAsync(dto, ct);
        return CreatedAtAction(nameof(GetPaymentStatuses), result);
    }

    /// <summary>Atualiza um status de pagamento personalizado. Somente Admin. Itens de sistema não podem ser alterados.</summary>
    [HttpPut("payment-statuses/{id:int}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(LookupResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdatePaymentStatus(
        int id, [FromBody] UpdatePaymentStatusDto dto, CancellationToken ct)
    {
        var result = await _updatePaymentStatus.ExecuteAsync(dto with { Id = id }, ct);
        return Ok(result);
    }

    /// <summary>Exclui um status de pagamento personalizado. Somente Admin. Itens de sistema não podem ser excluídos.</summary>
    [HttpDelete("payment-statuses/{id:int}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeletePaymentStatus(int id, CancellationToken ct)
    {
        await _deletePaymentStatus.ExecuteAsync(id, ct);
        return NoContent();
    }

    // ── Source Type ───────────────────────────────────────────────────────────

    /// <summary>Lista todos os tipos de fonte (seed + personalizados).</summary>
    [HttpGet("source-types")]
    [ProducesResponseType(typeof(IEnumerable<LookupResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSourceTypes(CancellationToken ct)
        => Ok(await _getSourceTypes.ExecuteAsync(ct));

    /// <summary>Cria um novo tipo de fonte personalizado. Somente Admin.</summary>
    [HttpPost("source-types")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(LookupResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateSourceType(
        [FromBody] CreateSourceTypeDto dto, CancellationToken ct)
    {
        var result = await _createSourceType.ExecuteAsync(dto, ct);
        return CreatedAtAction(nameof(GetSourceTypes), result);
    }

    /// <summary>Atualiza um tipo de fonte personalizado. Somente Admin. Itens de sistema não podem ser alterados.</summary>
    [HttpPut("source-types/{id:int}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(LookupResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateSourceType(
        int id, [FromBody] UpdateSourceTypeDto dto, CancellationToken ct)
    {
        var result = await _updateSourceType.ExecuteAsync(dto with { Id = id }, ct);
        return Ok(result);
    }

    /// <summary>Exclui um tipo de fonte personalizado. Somente Admin. Itens de sistema não podem ser excluídos.</summary>
    [HttpDelete("source-types/{id:int}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteSourceType(int id, CancellationToken ct)
    {
        await _deleteSourceType.ExecuteAsync(id, ct);
        return NoContent();
    }

    // ── Fortnight Type ────────────────────────────────────────────────────────

    /// <summary>Lista todos os tipos de quinzena (seed + personalizados).</summary>
    [HttpGet("fortnight-types")]
    [ProducesResponseType(typeof(IEnumerable<LookupResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFortnightTypes(CancellationToken ct)
        => Ok(await _getFortnightTypes.ExecuteAsync(ct));

    /// <summary>Cria um novo tipo de quinzena personalizado. Somente Admin.</summary>
    [HttpPost("fortnight-types")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(LookupResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateFortnightType(
        [FromBody] CreateFortnightTypeDto dto, CancellationToken ct)
    {
        var result = await _createFortnightType.ExecuteAsync(dto, ct);
        return CreatedAtAction(nameof(GetFortnightTypes), result);
    }

    /// <summary>Atualiza um tipo de quinzena personalizado. Somente Admin. Itens de sistema não podem ser alterados.</summary>
    [HttpPut("fortnight-types/{id:int}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(LookupResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateFortnightType(
        int id, [FromBody] UpdateFortnightTypeDto dto, CancellationToken ct)
    {
        var result = await _updateFortnightType.ExecuteAsync(dto with { Id = id }, ct);
        return Ok(result);
    }

    /// <summary>Exclui um tipo de quinzena personalizado. Somente Admin. Itens de sistema não podem ser excluídos.</summary>
    [HttpDelete("fortnight-types/{id:int}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteFortnightType(int id, CancellationToken ct)
    {
        await _deleteFortnightType.ExecuteAsync(id, ct);
        return NoContent();
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// ADMIN USERS CONTROLLER — gerenciamento de usuários
// ══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Gerenciamento de usuários pelo administrador.
/// Todos os endpoints requerem role Admin.
/// </summary>
[Route("api/v1/admin/users")]
[Authorize(Roles = "Admin")]
public sealed class AdminUsersController : ApiControllerBase
{
    private readonly GetUsersUseCase            _getUsers;
    private readonly ToggleUserActiveUseCase    _toggleActive;
    private readonly AssignRoleUseCase          _assignRole;
    private readonly RemoveRoleUseCase          _removeRole;
    private readonly ResetUserPasswordUseCase   _resetPassword;
    private readonly CreateUserByAdminUseCase   _createUser;
    private readonly UpdateUserByAdminUseCase   _updateUser;

    public AdminUsersController(
        GetUsersUseCase           getUsers,
        ToggleUserActiveUseCase   toggleActive,
        AssignRoleUseCase         assignRole,
        RemoveRoleUseCase         removeRole,
        ResetUserPasswordUseCase  resetPassword,
        CreateUserByAdminUseCase  createUser,
        UpdateUserByAdminUseCase  updateUser)
    {
        _getUsers      = getUsers;
        _toggleActive  = toggleActive;
        _assignRole    = assignRole;
        _removeRole    = removeRole;
        _resetPassword = resetPassword;
        _createUser    = createUser;
        _updateUser    = updateUser;
    }

    /// <summary>Cria um novo usuário com role padrão User.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(AdminUserResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateUser(
        [FromBody] CreateUserByAdminDto dto, CancellationToken ct)
    {
        var result = await _createUser.ExecuteAsync(dto, ct);
        return CreatedAtAction(nameof(GetAll), result);
    }

    /// <summary>Atualiza o nome de um usuário.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(AdminUserResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateUser(
        Guid id, [FromBody] UpdateUserByAdminDto dto, CancellationToken ct)
    {
        var result = await _updateUser.ExecuteAsync(dto with { UserId = id }, ct);
        return Ok(result);
    }

    /// <summary>Lista usuários do sistema com paginação e filtros opcionais.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<AdminUserResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] AdminUserFilterDto filter, CancellationToken ct)
        => Ok(await _getUsers.ExecuteAsync(filter, ct));

    /// <summary>
    /// Ativa ou desativa um usuário (toggle).
    /// Admin não pode desativar a si próprio.
    /// </summary>
    [HttpPatch("{id:guid}/toggle-active")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ToggleActive(Guid id, CancellationToken ct)
    {
        await _toggleActive.ExecuteAsync(id, CurrentUserId, ct);
        return NoContent();
    }

    /// <summary>Atribui uma role a um usuário.</summary>
    [HttpPost("{id:guid}/roles")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AssignRole(
        Guid id,
        [FromBody] AssignRoleDto dto,
        CancellationToken ct)
    {
        await _assignRole.ExecuteAsync(dto with { UserId = id }, ct);
        return NoContent();
    }

    /// <summary>
    /// Remove uma role de um usuário.
    /// Admin não pode remover a própria role Admin.
    /// </summary>
    [HttpDelete("{id:guid}/roles/{roleId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveRole(
        Guid id, int roleId, CancellationToken ct)
    {
        await _removeRole.ExecuteAsync(
            new RemoveRoleDto(id, roleId), CurrentUserId, ct);
        return NoContent();
    }

    /// <summary>
    /// Reseta a senha de um usuário.
    /// Admin não pode usar este endpoint para alterar a própria senha.
    /// </summary>
    [HttpPatch("{id:guid}/reset-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResetPassword(
        Guid id,
        [FromBody] ResetPasswordDto dto,
        CancellationToken ct)
    {
        await _resetPassword.ExecuteAsync(dto with { UserId = id }, CurrentUserId, ct);
        return NoContent();
    }
}
