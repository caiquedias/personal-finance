using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PersonalFinance.Application.DTOs.Auth;
using PersonalFinance.Application.UseCases.Admin;
using PersonalFinance.Application.UseCases.Auth;

namespace PersonalFinance.Api.Controllers.V1;

/// <summary>
/// Endpoints de autenticação e recuperação de conta. Todos AllowAnonymous.
/// Login agora inclui roles do usuário no JWT.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/v1/auth")]
public sealed class AuthController(
    RegisterUserUseCase registerUseCase,
    LoginWithRolesUseCase loginUseCase,
    RequestPasswordResetUseCase requestPasswordResetUseCase,
    CompletePasswordResetUseCase completePasswordResetUseCase,
    ConfirmEmailUseCase confirmEmailUseCase,
    ResendEmailVerificationUseCase resendEmailVerificationUseCase) : ControllerBase
{
    private readonly RegisterUserUseCase _registerUseCase = registerUseCase;
    private readonly LoginWithRolesUseCase _loginUseCase = loginUseCase;
    private readonly RequestPasswordResetUseCase _requestPasswordResetUseCase = requestPasswordResetUseCase;
    private readonly CompletePasswordResetUseCase _completePasswordResetUseCase = completePasswordResetUseCase;
    private readonly ConfirmEmailUseCase _confirmEmailUseCase = confirmEmailUseCase;
    private readonly ResendEmailVerificationUseCase _resendEmailVerificationUseCase = resendEmailVerificationUseCase;

    /// <summary>Registra um novo usuário.</summary>
    [HttpPost("register")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register(
        [FromBody] RegisterUserDto dto, CancellationToken ct)
    {
        await _registerUseCase.ExecuteAsync(dto, ct);
        // Resposta genérica idêntica para conta nova e e-mail já cadastrado (anti-enumeração)
        return Accepted(new GenericMessageResponseDto("Se o e-mail puder ser cadastrado, enviaremos um código de confirmação."));
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

    /// <summary>Solicita o código de redefinição de senha. Resposta idêntica para qualquer e-mail.</summary>
    [HttpPost("password/forgot")]
    [EnableRateLimiting("account-recovery")]
    [ProducesResponseType(typeof(GenericMessageResponseDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> ForgotPassword(
        [FromBody] EmailRequestDto dto, CancellationToken ct)
    {
        await _requestPasswordResetUseCase.ExecuteAsync(dto, ClientIp(), ct);
        return Accepted(new GenericMessageResponseDto(
            "Se o e-mail estiver cadastrado, enviaremos um código para redefinir a senha."));
    }

    /// <summary>Conclui a redefinição de senha com e-mail + código. Código inválido retorna 400.</summary>
    [HttpPost("password/reset")]
    [EnableRateLimiting("account-recovery")]
    [ProducesResponseType(typeof(GenericMessageResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> ResetPassword(
        [FromBody] CompletePasswordResetDto dto, CancellationToken ct)
    {
        await _completePasswordResetUseCase.ExecuteAsync(dto, ClientIp(), ct);
        return Ok(new GenericMessageResponseDto("Senha redefinida com sucesso."));
    }

    /// <summary>Confirma o e-mail com e-mail + código. Código inválido retorna 400.</summary>
    [HttpPost("email/confirm")]
    [EnableRateLimiting("account-recovery")]
    [ProducesResponseType(typeof(GenericMessageResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> ConfirmEmail(
        [FromBody] ConfirmEmailDto dto, CancellationToken ct)
    {
        await _confirmEmailUseCase.ExecuteAsync(dto, ct);
        return Ok(new GenericMessageResponseDto("E-mail confirmado com sucesso."));
    }

    /// <summary>Reenvia o código de confirmação. Resposta idêntica para qualquer e-mail.</summary>
    [HttpPost("email/resend")]
    [EnableRateLimiting("account-recovery")]
    [ProducesResponseType(typeof(GenericMessageResponseDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> ResendEmailConfirmation(
        [FromBody] EmailRequestDto dto, CancellationToken ct)
    {
        await _resendEmailVerificationUseCase.ExecuteAsync(dto, ct);
        return Accepted(new GenericMessageResponseDto(
            "Se o e-mail estiver pendente de confirmação, enviaremos um novo código."));
    }

    private string ClientIp() => HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
