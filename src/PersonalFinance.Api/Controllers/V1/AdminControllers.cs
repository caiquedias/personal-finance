using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalFinance.Application.DTOs.Admin;
using PersonalFinance.Application.UseCases.Admin;
using PersonalFinance.Application.UseCases.Config;

namespace PersonalFinance.Api.Controllers.V1;

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
    GetSourceTypesUseCase getSourceTypes,
    CreateSourceTypeUseCase createSourceType,
    GetFortnightTypesUseCase getFortnightTypes,
    CreateFortnightTypeUseCase createFortnightType) : ApiControllerBase
{
    private readonly GetPaymentStatusesUseCase _getPaymentStatuses = getPaymentStatuses;
    private readonly CreatePaymentStatusUseCase _createPaymentStatus = createPaymentStatus;
    private readonly GetSourceTypesUseCase _getSourceTypes = getSourceTypes;
    private readonly CreateSourceTypeUseCase _createSourceType = createSourceType;
    private readonly GetFortnightTypesUseCase _getFortnightTypes = getFortnightTypes;
    private readonly CreateFortnightTypeUseCase _createFortnightType = createFortnightType;

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
    private readonly GetUsersUseCase           _getUsers;
    private readonly ToggleUserActiveUseCase   _toggleActive;
    private readonly AssignRoleUseCase         _assignRole;
    private readonly RemoveRoleUseCase         _removeRole;
    private readonly ResetUserPasswordUseCase  _resetPassword;

    public AdminUsersController(
        GetUsersUseCase          getUsers,
        ToggleUserActiveUseCase  toggleActive,
        AssignRoleUseCase        assignRole,
        RemoveRoleUseCase        removeRole,
        ResetUserPasswordUseCase resetPassword)
    {
        _getUsers      = getUsers;
        _toggleActive  = toggleActive;
        _assignRole    = assignRole;
        _removeRole    = removeRole;
        _resetPassword = resetPassword;
    }

    /// <summary>Lista todos os usuários do sistema, incluindo inativos.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<AdminUserResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
        => Ok(await _getUsers.ExecuteAsync(ct));

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
