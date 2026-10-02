using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using PersonalFinance.Domain.Entities.Auth;
using PersonalFinance.Domain.Interfaces.Services;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace PersonalFinance.Infrastructure.Auth;

public sealed class JwtSettings
{
    public string SecretKey         { get; set; } = default!;
    public string Issuer            { get; set; } = default!;
    public string Audience          { get; set; } = default!;
    public int    ExpirationMinutes { get; set; } = 60;
}

/// <summary>
/// Gera JWT com claims: sub (userId), email, name, jti e role (uma por role).
/// Role claims habilitam [Authorize(Roles = "Admin")] nos controllers.
/// </summary>
public sealed class JwtTokenService : ITokenService
{
    /// <summary>Audience do token intermediário do 2º fator (distinta da do token completo).</summary>
    public const string MfaChallengeAudience = "pf-mfa";

    /// <summary>Claim que marca o token como pendente de 2º fator.</summary>
    public const string MfaPendingClaim = "mfa_pending";

    /// <summary>Claim com o SecurityStamp do usuário; validada a cada request para permitir revogação de sessões.</summary>
    public const string SecurityStampClaim = "stamp";

    private const int MfaChallengeMinutes = 5;

    private readonly JwtSettings _settings;

    public JwtTokenService(IOptions<JwtSettings> settings)
        => _settings = settings.Value;

    public string Generate(User user, IEnumerable<string> roles)
    {
        var key         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.SecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub,   user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Name,  user.Name),
            new(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString()),
            new(SecurityStampClaim,            user.SecurityStamp.ToString()),
        };

        // Uma claim "role" por role — ASP.NET Core lê automaticamente para [Authorize(Roles)]
        foreach (var role in roles)
            claims.Add(new Claim(ClaimTypes.Role, role));

        var token = new JwtSecurityToken(
            issuer:             _settings.Issuer,
            audience:           _settings.Audience,
            claims:             claims,
            expires:            DateTime.UtcNow.AddMinutes(_settings.ExpirationMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// Token intermediário do 2º fator: audience própria (rejeitada pelo esquema padrão), claims mínimas
    /// (sub, jti, mfa_pending), sem roles/e-mail/nome, validade de 5 minutos.
    /// </summary>
    public string GenerateMfaChallenge(User user)
    {
        var key         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.SecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(MfaPendingClaim, "true"),
        };

        var token = new JwtSecurityToken(
            issuer:             _settings.Issuer,
            audience:           MfaChallengeAudience,
            claims:             claims,
            expires:            DateTime.UtcNow.AddMinutes(MfaChallengeMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
