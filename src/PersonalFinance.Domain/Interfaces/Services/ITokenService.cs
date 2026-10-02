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

    /// <summary>
    /// Gera o token intermediário do 2º fator (MFA): audience distinta, claims mínimas
    /// (sub, jti, mfa_pending), sem roles, validade curta. Não autoriza endpoints comuns.
    /// </summary>
    string GenerateMfaChallenge(User user);
}
