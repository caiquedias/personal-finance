using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace PersonalFinance.Api.Controllers.V1;

/// <summary>
/// Controller base para todos os endpoints autenticados.
/// Centraliza a extração do UserId do token JWT e o prefixo de rota /api/v1.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/[controller]")]
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>
    /// Extrai o UserId do token JWT autenticado.
    /// Lança UnauthorizedAccessException se o claim não estiver presente.
    /// </summary>
    protected Guid CurrentUserId
    {
        get
        {
            var sub = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
                   ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(sub) || !Guid.TryParse(sub, out var userId))
                throw new UnauthorizedAccessException("Token inválido ou expirado.");

            return userId;
        }
    }
}
