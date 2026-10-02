using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PersonalFinance.Domain.Entities.Auth;
using PersonalFinance.Infrastructure.Persistence.Context;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using Xunit;
using static PersonalFinance.Api.Tests.Integration.MfaTestHelper;

namespace PersonalFinance.Api.Tests.Integration;

/// <summary>
/// 2º fator no login (#393) com Auth:Mfa:Enforce=true: challenge, /auth/mfa/verify,
/// isolamento do token intermediário, anti-replay, recovery code e lockout.
/// MaxFailedAttempts=3 para exercitar o bloqueio do par (conta, IP) sem dezenas de requests.
/// </summary>
public class MfaLoginFlowTests : IDisposable
{
    private readonly TestWebApplicationFactory _baseFactory = new();
    private readonly WebApplicationFactory<Program> _factory;

    public MfaLoginFlowTests()
    {
        var settings = new Dictionary<string, string?>
        {
            ["Auth:Mfa:Enforce"] = "true",
            ["Auth:LoginLockout:MaxFailedAttempts"] = "3"
        };
        _factory = _baseFactory.WithWebHostBuilder(b =>
        {
            foreach (var (key, value) in settings) b.UseSetting(key, value);
            b.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(settings));
        });
    }

    private HttpClient ChallengeClient(string mfaToken) => WithBearer(_factory, mfaToken);

    private async Task<HttpResponseMessage> VerifyAsync(string mfaToken, string code) =>
        await ChallengeClient(mfaToken).PostAsJsonAsync(VerifyPath, new { code });

    // ── Login: com e sem MFA ──────────────────────────────────────────────────

    [Fact(DisplayName = "Usuário sem MFA deve logar normalmente com Enforce=true")]
    public async Task Login_UserWithoutMfa_ShouldReturnFullToken()
    {
        var client = _factory.CreateClient();
        var email = await RegisterUserAsync(client);

        var response = await client.PostAsJsonAsync(LoginPath, new { email, password = Password });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(response);
        body.GetProperty("token").GetString().Should().NotBeNullOrWhiteSpace();
        (body.TryGetProperty("mfaRequired", out var required) && required.GetBoolean()).Should().BeFalse();
    }

    [Fact(DisplayName = "Usuário com MFA ativo deve receber MfaRequired + MfaToken e nenhum token completo")]
    public async Task Login_UserWithMfa_ShouldReturnChallengeOnly()
    {
        var mfa = await CreateMfaUserAsync(_factory);

        var response = await _factory.CreateClient().PostAsJsonAsync(LoginPath, new { email = mfa.Email, password = mfa.Password });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(response);
        body.GetProperty("mfaRequired").GetBoolean().Should().BeTrue();
        body.GetProperty("mfaToken").GetString().Should().NotBeNullOrWhiteSpace();
        (body.TryGetProperty("token", out var token) ? token.ValueKind : System.Text.Json.JsonValueKind.Null)
            .Should().Be(System.Text.Json.JsonValueKind.Null);
    }

    [Fact(DisplayName = "Senha errada com MFA ativo deve manter a mensagem genérica de credenciais inválidas")]
    public async Task Login_UserWithMfaWrongPassword_ShouldReturnGenericError()
    {
        var mfa = await CreateMfaUserAsync(_factory);
        var client = _factory.CreateClient();

        var wrong = await client.PostAsJsonAsync(LoginPath, new { email = mfa.Email, password = "Errada@123" });
        var unknown = await client.PostAsJsonAsync(LoginPath, new { email = "ninguem@monkeybomb.com", password = "Errada@123" });

        wrong.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadJsonAsync(wrong)).GetProperty("message").GetString().Should().Be("Credenciais inválidas.");
        (await ReadJsonAsync(unknown)).GetProperty("message").GetString().Should().Be("Credenciais inválidas.");
    }

    // ── Isolamento do token intermediário ─────────────────────────────────────

    [Fact(DisplayName = "Challenge token deve ser rejeitado (401) em endpoint [Authorize] qualquer")]
    public async Task ChallengeToken_OnProtectedEndpoint_ShouldReturn401()
    {
        var mfa = await CreateMfaUserAsync(_factory);
        var challenge = await GetChallengeTokenAsync(_factory, mfa);

        var response = await ChallengeClient(challenge).GetAsync("/api/v1/periods");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "Challenge token deve ser rejeitado (401) também nos endpoints de gestão de MFA")]
    public async Task ChallengeToken_OnMfaManagementEndpoints_ShouldReturn401()
    {
        var mfa = await CreateMfaUserAsync(_factory);
        var client = ChallengeClient(await GetChallengeTokenAsync(_factory, mfa));

        (await client.PostAsync(SetupPath, null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.PostAsJsonAsync(DisablePath, new { password = mfa.Password, code = ComputeCode(mfa.Secret, 30) }))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "Token completo deve ser rejeitado (401) em /auth/mfa/verify")]
    public async Task FullToken_OnVerify_ShouldReturn401()
    {
        var anonymous = _factory.CreateClient();
        var email = await RegisterUserAsync(anonymous);
        var login = await ReadJsonAsync(await anonymous.PostAsJsonAsync(LoginPath, new { email, password = Password }));
        var fullToken = login.GetProperty("token").GetString()!;

        var response = await WithBearer(_factory, fullToken).PostAsJsonAsync(VerifyPath, new { code = "123456" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "/auth/mfa/verify sem token deve retornar 401")]
    public async Task Verify_WithoutToken_ShouldReturn401()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync(VerifyPath, new { code = "123456" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── Verify: sucesso ───────────────────────────────────────────────────────

    [Fact(DisplayName = "Verify com TOTP válido deve devolver token completo utilizável nos endpoints protegidos")]
    public async Task Verify_WithValidTotp_ShouldReturnWorkingFullToken()
    {
        var mfa = await CreateMfaUserAsync(_factory);
        var challenge = await GetChallengeTokenAsync(_factory, mfa);

        var response = await VerifyAsync(challenge, ComputeCode(mfa.Secret, 30));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(response);
        var fullToken = body.GetProperty("token").GetString()!;
        body.GetProperty("email").GetString().Should().Be(mfa.Email);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(fullToken);
        jwt.Claims.Should().NotContain(c => c.Type == "mfa_pending");
        jwt.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Sub && c.Value == mfa.UserId.ToString());
        (await WithBearer(_factory, fullToken).GetAsync("/api/v1/periods")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "TOTP do mesmo step usado no enable não pode ser reaproveitado no verify (replay)")]
    public async Task Verify_WithCodeFromEnableStep_ShouldBeRejected()
    {
        var mfa = await CreateMfaUserAsync(_factory);
        var challenge = await GetChallengeTokenAsync(_factory, mfa);

        var response = await VerifyAsync(challenge, mfa.EnableCode);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "TOTP reutilizado no mesmo time step deve ser rejeitado")]
    public async Task Verify_ReusedTotp_ShouldBeRejected()
    {
        var mfa = await CreateMfaUserAsync(_factory);
        var code = ComputeCode(mfa.Secret, 30);

        var first = await VerifyAsync(await GetChallengeTokenAsync(_factory, mfa), code);
        var second = await VerifyAsync(await GetChallengeTokenAsync(_factory, mfa), code);

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        second.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await GetUserAsync(_factory, mfa.UserId)).LastUsedTotpStep.Should().NotBeNull();
    }

    [Fact(DisplayName = "Recovery code deve funcionar uma vez e ser rejeitado no 2º uso")]
    public async Task Verify_RecoveryCode_ShouldWorkOnlyOnce()
    {
        var mfa = await CreateMfaUserAsync(_factory);
        var recovery = mfa.RecoveryCodes[0];

        var first = await VerifyAsync(await GetChallengeTokenAsync(_factory, mfa), recovery);
        var second = await VerifyAsync(await GetChallengeTokenAsync(_factory, mfa), recovery);

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        second.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var used = await WithDbAsync(_factory, db =>
            db.Set<MfaRecoveryCode>().CountAsync(c => c.UserId == mfa.UserId && c.UsedAt != null));
        used.Should().Be(1);
    }

    // ── Verify: falhas e lockout ──────────────────────────────────────────────

    [Fact(DisplayName = "Código errado no verify deve retornar 400 e incrementar lockout global e do par (conta, IP)")]
    public async Task Verify_WithWrongCode_ShouldCountGlobalAndPairFailures()
    {
        var mfa = await CreateMfaUserAsync(_factory);
        var challenge = await GetChallengeTokenAsync(_factory, mfa);

        var response = await VerifyAsync(challenge, "000000");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await GetUserAsync(_factory, mfa.UserId)).FailedLoginCount.Should().Be(1);
        var pair = await WithDbAsync(_factory, db =>
            db.LoginThrottles.AsNoTracking().SingleAsync(t => t.UserId == mfa.UserId));
        pair.FailedCount.Should().Be(1);
    }

    [Fact(DisplayName = "Senha correta + código errado NÃO zera contadores; só o 2º fator correto zera")]
    public async Task Verify_ResetsCountersOnlyAfterSecondFactor()
    {
        var mfa = await CreateMfaUserAsync(_factory);
        await VerifyAsync(await GetChallengeTokenAsync(_factory, mfa), "000000");

        // Nova etapa de senha correta: o contador deve permanecer
        var challenge = await GetChallengeTokenAsync(_factory, mfa);
        (await GetUserAsync(_factory, mfa.UserId)).FailedLoginCount.Should().Be(1);

        var ok = await VerifyAsync(challenge, ComputeCode(mfa.Secret, 30));

        ok.StatusCode.Should().Be(HttpStatusCode.OK);
        (await GetUserAsync(_factory, mfa.UserId)).FailedLoginCount.Should().Be(0);
        (await WithDbAsync(_factory, db => db.LoginThrottles.CountAsync(t => t.UserId == mfa.UserId))).Should().Be(0);
    }

    [Fact(DisplayName = "Após MaxFailedAttempts falhas no verify o par é bloqueado mesmo com código válido")]
    public async Task Verify_AfterMaxFailures_ShouldLockEvenWithValidCode()
    {
        var mfa = await CreateMfaUserAsync(_factory);
        var challenge = await GetChallengeTokenAsync(_factory, mfa);
        for (var i = 0; i < 3; i++)
            (await VerifyAsync(challenge, "000000")).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var response = await VerifyAsync(challenge, ComputeCode(mfa.Secret, 30));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await WithDbAsync(_factory, db => db.LoginThrottles.AsNoTracking().SingleAsync(t => t.UserId == mfa.UserId)))
            .LockedUntil.Should().NotBeNull();
    }

    public void Dispose() => _baseFactory.Dispose();
}
