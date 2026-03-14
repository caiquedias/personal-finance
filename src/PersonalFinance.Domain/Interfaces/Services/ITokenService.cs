using PersonalFinance.Domain.Entities.Auth;

namespace PersonalFinance.Domain.Interfaces.Services;

/// <summary>
/// Serviço de geração de token JWT.
/// Roles são incluídas como claims para suportar [Authorize(Roles = "Admin")].
/// </summary>
public interface ITokenService
{
    /// <summary>
    /// Gera JWT assinado com claims: sub, email, name, jti e role (uma por role do usuário).
    /// </summary>
    string Generate(User user, IEnumerable<string> roles);
}
