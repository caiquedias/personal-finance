using Microsoft.AspNetCore.Mvc;
using PersonalFinance.Application.DTOs.Auth;
using PersonalFinance.Application.UseCases.Auth;

namespace PersonalFinance.Api.Controllers.V1;

/// <summary>
/// Gestão de MFA (TOTP) do usuário autenticado. Todos os endpoints exigem o token completo
/// (herdado de ApiControllerBase) e funcionam independente da flag Auth:Mfa:Enforce.
/// </summary>
[Route("api/v1/auth/mfa")]
public sealed class MfaController(
    SetupMfaUseCase setupUseCase,
    EnableMfaUseCase enableUseCase,
    DisableMfaUseCase disableUseCase) : ApiControllerBase
{
    private readonly SetupMfaUseCase _setupUseCase = setupUseCase;
    private readonly EnableMfaUseCase _enableUseCase = enableUseCase;
    private readonly DisableMfaUseCase _disableUseCase = disableUseCase;

    /// <summary>Gera o secret TOTP pendente e a URI otpauth. Com MFA já ativo, retorna 400.</summary>
    [HttpPost("setup")]
    [ProducesResponseType(typeof(MfaSetupResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Setup(CancellationToken ct)
        => Ok(await _setupUseCase.ExecuteAsync(CurrentUserId, ct));

    /// <summary>Valida o 1º código, ativa o MFA e devolve os recovery codes (exibidos uma única vez).</summary>
    [HttpPost("enable")]
    [ProducesResponseType(typeof(EnableMfaResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Enable([FromBody] EnableMfaDto dto, CancellationToken ct)
        => Ok(await _enableUseCase.ExecuteAsync(CurrentUserId, dto, ct));

    /// <summary>Desativa o MFA exigindo senha e (código TOTP ou recovery code).</summary>
    [HttpPost("disable")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Disable([FromBody] DisableMfaDto dto, CancellationToken ct)
    {
        await _disableUseCase.ExecuteAsync(CurrentUserId, dto, ct);
        return NoContent();
    }
}
