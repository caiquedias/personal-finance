using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using PersonalFinance.Domain.Interfaces.Repositories;
using PersonalFinance.Infrastructure.Auth;

namespace PersonalFinance.Api.Auth;

/// <summary>
/// Invalida sessões JWT via SecurityStamp (#488): a claim stamp do token deve bater com o stamp
/// atual do usuário. Claim ausente, divergente, usuário inativo ou removido → falha (401).
/// </summary>
public static class SecurityStampValidator
{
    public static async Task ValidateAsync(TokenValidatedContext context)
    {
        var principal = context.Principal;
        var sub = principal?.FindFirstValue(JwtRegisteredClaimNames.Sub)
               ?? principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        var stampClaim = principal?.FindFirstValue(JwtTokenService.SecurityStampClaim);

        if (!Guid.TryParse(sub, out var userId) || !Guid.TryParse(stampClaim, out var tokenStamp))
        {
            context.Fail("Token sem identificação ou stamp válidos.");
            return;
        }

        var repository = context.HttpContext.RequestServices.GetRequiredService<IUserRepository>();
        var currentStamp = await repository.GetSecurityStampAsync(userId, context.HttpContext.RequestAborted);

        if (currentStamp is null || currentStamp.Value != tokenStamp)
            context.Fail("Sessão invalidada.");
    }
}
