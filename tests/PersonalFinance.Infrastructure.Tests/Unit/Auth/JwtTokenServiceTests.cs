using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using PersonalFinance.Domain.Entities.Auth;
using PersonalFinance.Infrastructure.Auth;
using Xunit;

namespace PersonalFinance.Infrastructure.Tests.Unit.Auth;

/// <summary>
/// JwtTokenService — foco no token intermediário do 2º fator (#393): aud distinta, claims mínimas,
/// sem roles, TTL de 5 minutos. O token completo (Generate) segue com audience própria.
/// </summary>
public class JwtTokenServiceTests
{
    private const string Key = "TestOnly_9f8e7d6c5b4a3210_FakeJwtSecretKey_NotForProd";
    private const string MfaAudience = "pf-mfa";

    private static readonly JwtSettings Settings = new()
    {
        SecretKey = Key, Issuer = "pf-issuer", Audience = "pf-api", ExpirationMinutes = 60
    };

    private readonly JwtTokenService _sut = new(Options.Create(Settings));

    private static User FakeUser() => User.Create("Caique", "caique@monkeybomb.com", "hashed_password");

    private static JwtSecurityToken Read(string token) => new JwtSecurityTokenHandler().ReadJwtToken(token);

    private static TokenValidationParameters ParamsFor(string audience) => new()
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key)),
        ValidateIssuer = true, ValidIssuer = "pf-issuer",
        ValidateAudience = true, ValidAudience = audience,
        ValidateLifetime = true, ClockSkew = TimeSpan.Zero
    };

    [Fact(DisplayName = "Challenge deve usar audience distinta da do token completo")]
    public void GenerateMfaChallenge_ShouldUseDistinctAudience()
    {
        var challenge = Read(_sut.GenerateMfaChallenge(FakeUser()));
        var full = Read(_sut.Generate(FakeUser(), new[] { "User" }));

        challenge.Audiences.Should().ContainSingle().Which.Should().Be(MfaAudience);
        full.Audiences.Should().ContainSingle().Which.Should().Be("pf-api");
        challenge.Issuer.Should().Be("pf-issuer");
    }

    [Fact(DisplayName = "Challenge deve ter claims mínimas: sub, jti e mfa_pending")]
    public void GenerateMfaChallenge_ShouldHaveMinimalClaims()
    {
        var user = FakeUser();

        var token = Read(_sut.GenerateMfaChallenge(user));

        token.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Sub).Value.Should().Be(user.Id.ToString());
        token.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Jti).Value.Should().NotBeNullOrWhiteSpace();
        token.Claims.Single(c => c.Type == "mfa_pending").Value.Should().Be("true");
    }

    [Fact(DisplayName = "Challenge não deve conter roles, e-mail nem nome")]
    public void GenerateMfaChallenge_ShouldNotContainRolesOrProfile()
    {
        var token = Read(_sut.GenerateMfaChallenge(FakeUser()));

        token.Claims.Should().NotContain(c =>
            c.Type == ClaimTypes.Role || c.Type == "role" ||
            c.Type == JwtRegisteredClaimNames.Email || c.Type == JwtRegisteredClaimNames.Name);
    }

    [Fact(DisplayName = "Challenge deve expirar em 5 minutos")]
    public void GenerateMfaChallenge_ShouldExpireInFiveMinutes()
    {
        var before = DateTime.UtcNow;

        var token = Read(_sut.GenerateMfaChallenge(FakeUser()));

        token.ValidTo.Should().BeCloseTo(before.AddMinutes(5), TimeSpan.FromSeconds(10));
    }

    [Fact(DisplayName = "Challenge deve ser assinado com a chave configurada e válido para a audience pf-mfa")]
    public void GenerateMfaChallenge_ShouldBeSignedWithConfiguredKey()
    {
        var raw = _sut.GenerateMfaChallenge(FakeUser());

        var result = new JwtSecurityTokenHandler().ValidateToken(raw, ParamsFor(MfaAudience), out _);

        result.Should().NotBeNull();
    }

    [Fact(DisplayName = "Challenge NÃO deve ser válido com a audience do token completo")]
    public void GenerateMfaChallenge_ShouldFailValidationAgainstDefaultAudience()
    {
        var raw = _sut.GenerateMfaChallenge(FakeUser());

        var act = () => new JwtSecurityTokenHandler().ValidateToken(raw, ParamsFor("pf-api"), out _);

        act.Should().Throw<SecurityTokenInvalidAudienceException>();
    }

    [Fact(DisplayName = "Dois challenges do mesmo usuário devem ter jti distintos")]
    public void GenerateMfaChallenge_ShouldHaveUniqueJti()
    {
        var user = FakeUser();

        var a = Read(_sut.GenerateMfaChallenge(user)).Id;
        var b = Read(_sut.GenerateMfaChallenge(user)).Id;

        a.Should().NotBe(b);
    }

    [Fact(DisplayName = "Token completo deve continuar com claims de perfil e roles (regressão)")]
    public void Generate_ShouldKeepProfileAndRoleClaims()
    {
        var token = Read(_sut.Generate(FakeUser(), new[] { "Admin", "User" }));

        token.Claims.Where(c => c.Type == ClaimTypes.Role || c.Type == "role").Select(c => c.Value)
            .Should().BeEquivalentTo(new[] { "Admin", "User" });
        token.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Email);
        token.Claims.Should().NotContain(c => c.Type == "mfa_pending");
    }

    [Fact(DisplayName = "Token completo deve incluir a claim stamp com o SecurityStamp do usuário (#488)")]
    public void Generate_ShouldIncludeSecurityStampClaim()
    {
        var user = FakeUser();

        var token = Read(_sut.Generate(user, new[] { "User" }));

        JwtTokenService.SecurityStampClaim.Should().Be("stamp");
        token.Claims.Single(c => c.Type == "stamp").Value.Should().Be(user.SecurityStamp.ToString());
    }

    [Fact(DisplayName = "Challenge MFA não deve conter a claim stamp (#488)")]
    public void GenerateMfaChallenge_ShouldNotContainSecurityStamp()
    {
        var token = Read(_sut.GenerateMfaChallenge(FakeUser()));

        token.Claims.Should().NotContain(c => c.Type == "stamp");
    }
}
