using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PersonalFinance.Application.DTOs.Auth;
using PersonalFinance.Application.UseCases.Auth;

namespace PersonalFinance.Api.Controllers.V1;

/// <summary>
/// 2º fator do login. Aceita SOMENTE o token intermediário (esquema "MfaChallenge");
/// o token completo é rejeitado (401). Controller separado do MfaController porque o
/// [Authorize] de classe da ApiControllerBase combinaria (AND) com o esquema do challenge.
/// </summary>
[ApiController]
[Authorize(AuthenticationSchemes = MfaVerifyController.ChallengeScheme)]
[Route("api/v1/auth/mfa")]
public sealed class MfaVerifyController(VerifyMfaUseCase verifyUseCase) : ControllerBase
{
    /// <summary>Nome do esquema JwtBearer do token intermediário.</summary>
    public const string ChallengeScheme = "MfaChallenge";

    private readonly VerifyMfaUseCase _verifyUseCase = verifyUseCase;

    /// <summary>Valida TOTP ou recovery code e devolve o token completo.</summary>
    [HttpPost("verify")]
    [EnableRateLimiting("mfa-verify")]
    [ProducesResponseType(typeof(LoginResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Verify([FromBody] VerifyMfaDto dto, CancellationToken ct)
    {
        var sub = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
               ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(sub, out var userId))
            return Unauthorized();

        var result = await _verifyUseCase.ExecuteAsync(
            userId, dto, HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown", ct);
        return Ok(result);
    }
}
