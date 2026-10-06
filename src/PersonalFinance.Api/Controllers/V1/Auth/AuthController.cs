using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PersonalFinance.Application.DTOs.Auth;
using PersonalFinance.Application.UseCases.Admin;
using PersonalFinance.Application.UseCases.Auth;

namespace PersonalFinance.Api.Controllers.V1;

/// <summary>
/// Endpoints de autenticação. Ambos AllowAnonymous.
/// Login agora inclui roles do usuário no JWT.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/v1/auth")]
public sealed class AuthController(
    RegisterUserUseCase registerUseCase,
    LoginWithRolesUseCase loginUseCase) : ControllerBase
{
    private readonly RegisterUserUseCase _registerUseCase = registerUseCase;
    private readonly LoginWithRolesUseCase _loginUseCase = loginUseCase;

    /// <summary>Registra um novo usuário.</summary>
    [HttpPost("register")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register(
        [FromBody] RegisterUserDto dto, CancellationToken ct)
    {
        await _registerUseCase.ExecuteAsync(dto, ct);
        // Resposta genérica idêntica para conta nova e e-mail já cadastrado (anti-enumeração)
        return Accepted(new { message = "Se o e-mail puder ser cadastrado, enviaremos um código de confirmação." });
    }

    /// <summary>
    /// Autentica o usuário e retorna JWT com roles incluídas.
    /// Use o token em endpoints com [Authorize(Roles = "Admin")].
    /// </summary>
    [HttpPost("login")]
    [EnableRateLimiting("login")]
    [ProducesResponseType(typeof(LoginResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Login(
        [FromBody] LoginDto dto, CancellationToken ct)
    {
        var result = await _loginUseCase.ExecuteAsync(
            dto, HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown", ct);
        return Ok(result);
    }
}
