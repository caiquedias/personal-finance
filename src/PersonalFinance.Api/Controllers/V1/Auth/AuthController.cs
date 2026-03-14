using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
    [ProducesResponseType(typeof(UserResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register(
        [FromBody] RegisterUserDto dto, CancellationToken ct)
    {
        var result = await _registerUseCase.ExecuteAsync(dto, ct);
        return CreatedAtAction(nameof(Register), new { id = result.Id }, result);
    }

    /// <summary>
    /// Autentica o usuário e retorna JWT com roles incluídas.
    /// Use o token em endpoints com [Authorize(Roles = "Admin")].
    /// </summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Login(
        [FromBody] LoginDto dto, CancellationToken ct)
    {
        var result = await _loginUseCase.ExecuteAsync(dto, ct);
        return Ok(result);
    }
}
