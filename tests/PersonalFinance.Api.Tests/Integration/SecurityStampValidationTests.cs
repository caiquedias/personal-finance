using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Xunit;
using static PersonalFinance.Api.Tests.Integration.MfaTestHelper;

namespace PersonalFinance.Api.Tests.Integration;

/// <summary>
/// Invalidação de sessões JWT via SecurityStamp (#488): a claim stamp do token deve bater com o
/// stamp atual do usuário; ausente, divergente, usuário inativo ou removido → 401.
/// </summary>
public class SecurityStampValidationTests : IDisposable
{
    private const string Protected = "/api/v1/periods";
    private const string Key = "TestOnly_9f8e7d6c5b4a3210_FakeJwtSecretKey_NotForProd";

    private readonly TestWebApplicationFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private async Task<(string Email, string Token, Guid UserId)> LoginAsync()
    {
        var anonymous = _factory.CreateClient();
        var email = await RegisterUserAsync(anonymous);
        var login = await ReadJsonAsync(await anonymous.PostAsJsonAsync(LoginPath, new { email, password = Password }));
        var userId = await WithDbAsync(_factory, db => db.Users.Where(u => u.Email == email).Select(u => u.Id).SingleAsync());
        return (email, login.GetProperty("token").GetString()!, userId);
    }

    private Task RotateStampAsync(Guid userId) => WithDbAsync(_factory, async db =>
    {
        var user = await db.Users.SingleAsync(u => u.Id == userId);
        user.RotateSecurityStamp();
        await db.SaveChangesAsync();
        return 0;
    });

    private static string TokenWithoutStamp(Guid userId)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, "User")
        };
        var creds = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key)), SecurityAlgorithms.HmacSha256);
        var jwt = new JwtSecurityToken("PersonalFinance", "PersonalFinance", claims,
            expires: DateTime.UtcNow.AddMinutes(10), signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }

    [Fact(DisplayName = "Token com stamp atual deve acessar endpoint protegido (200)")]
    public async Task ValidStamp_ShouldReturn200()
    {
        var (_, token, _) = await LoginAsync();

        var response = await WithBearer(_factory, token).GetAsync(Protected);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "Token emitido antes da rotação do stamp deve ser rejeitado (401)")]
    public async Task RotatedStamp_ShouldReturn401()
    {
        var (_, token, userId) = await LoginAsync();
        await RotateStampAsync(userId);

        var response = await WithBearer(_factory, token).GetAsync(Protected);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "Novo login após a rotação deve gerar token válido (200)")]
    public async Task LoginAfterRotation_ShouldReturn200()
    {
        var (email, _, userId) = await LoginAsync();
        await RotateStampAsync(userId);

        var login = await ReadJsonAsync(await _factory.CreateClient()
            .PostAsJsonAsync(LoginPath, new { email, password = Password }));
        var response = await WithBearer(_factory, login.GetProperty("token").GetString()!).GetAsync(Protected);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "Token sem a claim stamp deve ser rejeitado (401), sem bypass")]
    public async Task MissingStampClaim_ShouldReturn401()
    {
        var (_, _, userId) = await LoginAsync();

        var response = await WithBearer(_factory, TokenWithoutStamp(userId)).GetAsync(Protected);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "Usuário desativado deve ter o token rejeitado (401)")]
    public async Task InactiveUser_ShouldReturn401()
    {
        var (_, token, userId) = await LoginAsync();
        await WithDbAsync(_factory, async db =>
        {
            var user = await db.Users.SingleAsync(u => u.Id == userId);
            user.Deactivate();
            await db.SaveChangesAsync();
            return 0;
        });

        var response = await WithBearer(_factory, token).GetAsync(Protected);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "Usuário soft-deleted deve ter o token rejeitado (401)")]
    public async Task SoftDeletedUser_ShouldReturn401()
    {
        var (_, token, userId) = await LoginAsync();
        await WithDbAsync(_factory, async db =>
        {
            var user = await db.Users.SingleAsync(u => u.Id == userId);
            user.SoftDelete();
            await db.SaveChangesAsync();
            return 0;
        });

        var response = await WithBearer(_factory, token).GetAsync(Protected);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
