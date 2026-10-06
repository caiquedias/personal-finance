using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PersonalFinance.Api.Tests.Integration.Fakes;
using PersonalFinance.Domain.Entities.Auth;
using PersonalFinance.Domain.Enums;
using PersonalFinance.Infrastructure.Persistence.Context;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;
using static PersonalFinance.Api.Tests.Integration.MfaTestHelper;

namespace PersonalFinance.Api.Tests.Integration;

/// <summary>
/// Reset de senha self-service ponta a ponta (#404): endpoints anônimos com resposta genérica
/// (anti-enumeração), código de 6 dígitos por e-mail (fila + FakeEmailSender), máx. de tentativas, uso único,
/// efeitos do reset concluído, cooldown por conta e ausência de segredos em logs/AuditLog.
/// </summary>
public class AccountRecoveryTests : IDisposable
{
    private const string ForgotPath = "/api/v1/auth/password/forgot";
    private const string ResetPath = "/api/v1/auth/password/reset";
    private const string LoginPath = "/api/v1/auth/login";
    private const string RegisterPath = "/api/v1/auth/register";
    private const string OldPassword = "Senha@Antiga123";
    private const string NewPassword = "NovaSenha@456";
    private const string InvalidCodeMessage = "Código inválido ou expirado.";

    private readonly TestWebApplicationFactory _baseFactory = new();
    private readonly CapturingLoggerProvider _logs = new();
    private readonly Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public AccountRecoveryTests()
    {
        _factory = _baseFactory.WithWebHostBuilder(b => b.ConfigureLogging(l => l.AddProvider(_logs)));
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _baseFactory.Dispose();
    }

    private FakeEmailSender Mail => _baseFactory.EmailSender;

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>E-mail só com letras (sem sequências numéricas que confundiriam a extração do código).</summary>
    private static string NewEmail(string prefix)
    {
        var letters = new string(Guid.NewGuid().ToByteArray().Select(b => (char)('a' + b % 26)).ToArray());
        return $"{prefix}{letters}@monkeybomb.com";
    }

    private async Task<string> RegisterUserAsync(string prefix)
    {
        var email = NewEmail(prefix);
        var response = await _client.PostAsJsonAsync(RegisterPath, new { name = "Conta Teste", email, password = OldPassword });
        response.EnsureSuccessStatusCode();
        return email;
    }

    private Task<HttpResponseMessage> ForgotAsync(string email) => _client.PostAsJsonAsync(ForgotPath, new { email });

    private Task<HttpResponseMessage> ResetAsync(string email, string code, string newPassword = NewPassword) =>
        _client.PostAsJsonAsync(ResetPath, new { email, code, newPassword });

    private Task<HttpResponseMessage> LoginAsync(string email, string password) =>
        _client.PostAsJsonAsync(LoginPath, new { email, password });

    private async Task<string> ForgotAndGetCodeAsync(string email)
    {
        (await ForgotAsync(email)).StatusCode.Should().Be(HttpStatusCode.Accepted);
        return await Mail.WaitForCodeAsync(email, "/reset-password");
    }

    private Task<T> WithDbAsync<T>(Func<AppDbContext, Task<T>> action) => MfaTestHelper.WithDbAsync(_factory, action);

    private static string WrongCodeFor(string code) => code == "000000" ? "999999" : "000000";

    private static async Task<string> MessageOf(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("message").GetString()!;
    }

    // ── POST /password/forgot ─────────────────────────────────────────────────

    [Fact(DisplayName = "forgot: usuário existente recebe 202 genérico e um e-mail com código de 6 dígitos e link sem código")]
    public async Task Forgot_ExistingUser_ShouldReturn202AndEmailCode()
    {
        var email = await RegisterUserAsync("fp");

        var response = await ForgotAsync(email);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        (await MessageOf(response)).Should().NotBeNullOrWhiteSpace();
        var code = await Mail.WaitForCodeAsync(email, "/reset-password");
        code.Should().MatchRegex("^[0-9]{6}$");
        var html = Mail.SentTo(email, "/reset-password").Last().HtmlBody;
        html.Should().Contain($"{TestWebApplicationFactory.TestFrontendBaseUrl}/reset-password#email={Uri.EscapeDataString(email)}");
        Regex.Matches(html, "href=\"([^\"]*)\"").Select(m => m.Groups[1].Value).Should().OnlyContain(h => !h.Contains(code));
    }

    [Fact(DisplayName = "forgot: e-mail inexistente responde idêntico ao existente (status e corpo) e não envia e-mail")]
    public async Task Forgot_UnknownEmail_ShouldBeIndistinguishableAndSendNothing()
    {
        var existing = await RegisterUserAsync("fpex");
        var unknown = NewEmail("ghost");

        var forExisting = await ForgotAsync(existing);
        var forUnknown = await ForgotAsync(unknown);
        await Mail.DrainAsync(_factory.Services);

        forUnknown.StatusCode.Should().Be(forExisting.StatusCode).And.Be(HttpStatusCode.Accepted);
        (await forUnknown.Content.ReadAsStringAsync()).Should().Be(await forExisting.Content.ReadAsStringAsync());
        Mail.SentTo(unknown, "/reset-password").Should().BeEmpty();
    }

    [Fact(DisplayName = "forgot: usuário inativo responde idêntico e não recebe e-mail")]
    public async Task Forgot_InactiveUser_ShouldBeIndistinguishableAndSendNothing()
    {
        var email = await RegisterUserAsync("fpin");
        await WithDbAsync(async db =>
        {
            var user = await db.Users.SingleAsync(u => u.Email == email);
            user.Deactivate();
            await db.SaveChangesAsync();
            return 0;
        });

        var response = await ForgotAsync(email);
        await Mail.DrainAsync(_factory.Services);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        Mail.SentTo(email, "/reset-password").Should().BeEmpty();
    }

    [Theory(DisplayName = "forgot: e-mail ausente ou inválido retorna 400")]
    [InlineData("")]
    [InlineData("nao_e_email")]
    public async Task Forgot_InvalidEmail_ShouldReturn400(string email)
    {
        (await ForgotAsync(email)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "forgot: segundo pedido dentro do cooldown responde igual mas não envia segundo e-mail")]
    public async Task Forgot_WithinCooldown_ShouldRespondSameButSendOnlyOne()
    {
        var email = await RegisterUserAsync("fpcd");

        var first = await ForgotAsync(email);
        var second = await ForgotAsync(email);
        await Mail.DrainAsync(_factory.Services);

        second.StatusCode.Should().Be(first.StatusCode).And.Be(HttpStatusCode.Accepted);
        (await second.Content.ReadAsStringAsync()).Should().Be(await first.Content.ReadAsStringAsync());
        Mail.SentTo(email, "/reset-password").Should().HaveCount(1);
    }

    [Fact(DisplayName = "forgot: endpoint é anônimo (sem token não retorna 401)")]
    public async Task Forgot_ShouldBeAnonymous()
    {
        var response = await ForgotAsync(NewEmail("anon"));

        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
    }

    // ── POST /password/reset ──────────────────────────────────────────────────

    [Fact(DisplayName = "reset: código correto troca a senha — nova senha loga e a antiga é recusada")]
    public async Task Reset_WithCorrectCode_ShouldChangePassword()
    {
        var email = await RegisterUserAsync("rs");
        var code = await ForgotAndGetCodeAsync(email);

        var response = await ResetAsync(email, code);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await LoginAsync(email, NewPassword)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await LoginAsync(email, OldPassword)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "reset: token JWT emitido antes do reset deixa de valer (401 via SecurityStamp)")]
    public async Task Reset_ShouldInvalidatePreviousJwt()
    {
        var email = await RegisterUserAsync("rsjwt");
        var login = await ReadJsonAsync(await LoginAsync(email, OldPassword));
        var oldToken = login.GetProperty("token").GetString()!;
        (await WithBearer(_factory, oldToken).GetAsync("/api/v1/periods")).StatusCode.Should().Be(HttpStatusCode.OK);
        var code = await ForgotAndGetCodeAsync(email);

        (await ResetAsync(email, code)).StatusCode.Should().Be(HttpStatusCode.OK);

        (await WithBearer(_factory, oldToken).GetAsync("/api/v1/periods")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "reset: confirma o e-mail, zera falhas/bloqueio e marca o token como usado")]
    public async Task Reset_ShouldConfirmEmailClearLockoutAndConsumeToken()
    {
        var email = await RegisterUserAsync("rsfx");
        await WithDbAsync(async db =>
        {
            var user = await db.Users.SingleAsync(u => u.Email == email);
            user.RegisterFailedLogin(1, TimeSpan.FromMinutes(15), DateTime.UtcNow); // bloqueia a conta
            await db.SaveChangesAsync();
            return 0;
        });
        (await LoginAsync(email, OldPassword)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var code = await ForgotAndGetCodeAsync(email);

        (await ResetAsync(email, code)).StatusCode.Should().Be(HttpStatusCode.OK);

        var (user, token) = await WithDbAsync(async db =>
        {
            var u = await db.Users.AsNoTracking().SingleAsync(x => x.Email == email);
            var t = await db.Set<UserToken>().AsNoTracking()
                .SingleAsync(x => x.UserId == u.Id && x.Purpose == UserTokenPurpose.PasswordReset);
            return (u, t);
        });
        user.IsEmailConfirmed.Should().BeTrue();
        user.FailedLoginCount.Should().Be(0);
        user.LockedUntil.Should().BeNull();
        token.UsedAt.Should().NotBeNull();
        (await LoginAsync(email, NewPassword)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "reset: o código nunca é guardado em claro (só HMAC) e o token expira em 10 minutos")]
    public async Task Reset_Token_ShouldStoreOnlyHashWithTtl()
    {
        var email = await RegisterUserAsync("rshash");
        var code = await ForgotAndGetCodeAsync(email);

        var token = await WithDbAsync(async db =>
        {
            var userId = await db.Users.Where(u => u.Email == email).Select(u => u.Id).SingleAsync();
            return await db.Set<UserToken>().AsNoTracking()
                .SingleAsync(t => t.UserId == userId && t.Purpose == UserTokenPurpose.PasswordReset);
        });

        token.TokenHash.Should().NotBeNullOrWhiteSpace();
        token.TokenHash.Should().NotContain(code).And.NotBe(code);
        (token.ExpiresAt - token.CreatedAt).Should().Be(TimeSpan.FromMinutes(10));
        token.Attempts.Should().Be(0);
    }

    [Fact(DisplayName = "reset: código errado retorna 400 (nunca 401) com a mensagem genérica e não troca a senha")]
    public async Task Reset_WrongCode_ShouldReturn400WithGenericMessage()
    {
        var email = await RegisterUserAsync("rswr");
        var code = await ForgotAndGetCodeAsync(email);

        var response = await ResetAsync(email, WrongCodeFor(code));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await MessageOf(response)).Should().Be(InvalidCodeMessage);
        (await LoginAsync(email, OldPassword)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "reset: 3 códigos errados invalidam o token — o código certo depois é recusado")]
    public async Task Reset_ThreeWrongCodes_ShouldInvalidateToken()
    {
        var email = await RegisterUserAsync("rs3x");
        var code = await ForgotAndGetCodeAsync(email);

        for (var i = 0; i < 3; i++)
            (await ResetAsync(email, WrongCodeFor(code))).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var late = await ResetAsync(email, code);

        late.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await MessageOf(late)).Should().Be(InvalidCodeMessage);
        (await LoginAsync(email, OldPassword)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await LoginAsync(email, NewPassword)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "reset: 2 códigos errados não invalidam — o certo na 3ª tentativa ainda funciona")]
    public async Task Reset_TwoWrongCodes_ShouldStillAcceptCorrect()
    {
        var email = await RegisterUserAsync("rs2x");
        var code = await ForgotAndGetCodeAsync(email);

        await ResetAsync(email, WrongCodeFor(code));
        await ResetAsync(email, WrongCodeFor(code));

        (await ResetAsync(email, code)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "reset: o código é de uso único — reutilizar após sucesso retorna 400")]
    public async Task Reset_ReusingCode_ShouldReturn400()
    {
        var email = await RegisterUserAsync("rsre");
        var code = await ForgotAndGetCodeAsync(email);
        (await ResetAsync(email, code)).StatusCode.Should().Be(HttpStatusCode.OK);

        var again = await ResetAsync(email, code, "OutraSenha@789");

        again.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await LoginAsync(email, NewPassword)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await LoginAsync(email, "OutraSenha@789")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "reset: token expirado retorna 400 com a mesma mensagem")]
    public async Task Reset_ExpiredToken_ShouldReturn400()
    {
        var email = await RegisterUserAsync("rsexp");
        var code = await ForgotAndGetCodeAsync(email);
        await WithDbAsync(async db =>
        {
            // Recria o token vencido preservando o hash: expiração no passado via SQL do provider InMemory
            var userId = await db.Users.Where(u => u.Email == email).Select(u => u.Id).SingleAsync();
            var old = await db.Set<UserToken>().SingleAsync(t => t.UserId == userId && t.Purpose == UserTokenPurpose.PasswordReset);
            db.Entry(old).Property(nameof(UserToken.ExpiresAt)).CurrentValue = DateTime.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
            return 0;
        });

        var response = await ResetAsync(email, code);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await MessageOf(response)).Should().Be(InvalidCodeMessage);
    }

    [Fact(DisplayName = "reset: e-mail inexistente responde 400 idêntico ao código errado de conta existente")]
    public async Task Reset_UnknownEmail_ShouldBeIndistinguishableFromWrongCode()
    {
        var email = await RegisterUserAsync("rsid");
        var code = await ForgotAndGetCodeAsync(email);

        var wrong = await ResetAsync(email, WrongCodeFor(code));
        var unknown = await ResetAsync(NewEmail("nobody"), "123456");

        unknown.StatusCode.Should().Be(wrong.StatusCode).And.Be(HttpStatusCode.BadRequest);
        (await MessageOf(unknown)).Should().Be(await MessageOf(wrong)); // o corpo traz traceId, que difere por request
    }

    [Fact(DisplayName = "reset: token de verificação de e-mail não serve para resetar senha (propósito diferente)")]
    public async Task Reset_WithVerificationCode_ShouldReturn400()
    {
        var email = await RegisterUserAsync("rsprp"); // register emite código de VERIFICAÇÃO
        var verificationCode = await Mail.WaitForCodeAsync(email, "/confirm-email");

        var response = await ResetAsync(email, verificationCode);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await LoginAsync(email, OldPassword)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory(DisplayName = "reset: payload inválido (código fora de 6 dígitos / senha curta ou longa) retorna 400")]
    [InlineData("12345", "NovaSenha@456")]
    [InlineData("1234567", "NovaSenha@456")]
    [InlineData("12345a", "NovaSenha@456")]
    [InlineData("123456", "curta")]
    public async Task Reset_InvalidPayload_ShouldReturn400(string code, string newPassword)
    {
        var response = await ResetAsync(NewEmail("inv"), code, newPassword);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "reset: senha acima de 128 caracteres retorna 400")]
    public async Task Reset_HugePassword_ShouldReturn400()
    {
        var response = await ResetAsync(NewEmail("huge"), "123456", new string('a', 129));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── MFA continua ativo ────────────────────────────────────────────────────

    [Fact(DisplayName = "reset: usuário com MFA continua exigindo o 2º fator no login após o reset")]
    public async Task Reset_ShouldKeepMfaRequired()
    {
        var settings = new Dictionary<string, string?> { ["Auth:Mfa:Enforce"] = "true" };
        var mfaFactory = _baseFactory.WithWebHostBuilder(b =>
        {
            foreach (var (key, value) in settings) b.UseSetting(key, value);
            b.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(settings));
        });
        var mfa = await CreateMfaUserAsync(mfaFactory);
        var client = mfaFactory.CreateClient();

        (await client.PostAsJsonAsync(ForgotPath, new { email = mfa.Email })).StatusCode.Should().Be(HttpStatusCode.Accepted);
        var code = await Mail.WaitForCodeAsync(mfa.Email, "/reset-password");
        (await client.PostAsJsonAsync(ResetPath, new { email = mfa.Email, code, newPassword = NewPassword }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var login = await client.PostAsJsonAsync(LoginPath, new { email = mfa.Email, password = NewPassword });

        login.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(login);
        body.GetProperty("mfaRequired").GetBoolean().Should().BeTrue();
        (body.TryGetProperty("token", out var token) && token.ValueKind == JsonValueKind.String).Should().BeFalse();
        (await GetUserAsync(mfaFactory, mfa.UserId)).MfaEnabled.Should().BeTrue();
    }

    // ── Auditoria ─────────────────────────────────────────────────────────────

    [Fact(DisplayName = "auditoria: forgot grava PasswordResetRequested e reset grava PasswordResetCompleted (ator = alvo), sem código")]
    public async Task Audit_ShouldRecordRequestedAndCompleted()
    {
        var email = await RegisterUserAsync("aud");
        var code = await ForgotAndGetCodeAsync(email);
        (await ResetAsync(email, code)).StatusCode.Should().Be(HttpStatusCode.OK);

        var logs = await WithDbAsync(async db =>
        {
            var userId = await db.Users.Where(u => u.Email == email).Select(u => u.Id).SingleAsync();
            return await db.AuditLogs.AsNoTracking().Where(a => a.TargetUserId == userId).ToListAsync();
        });

        logs.Select(l => l.Action).Should().Contain(new[]
            { AuditAction.PasswordResetRequested, AuditAction.PasswordResetCompleted });
        logs.Should().OnlyContain(l => l.ActorUserId == l.TargetUserId);
        logs.Should().OnlyContain(l => l.Details == null || (!l.Details.Contains(code) && !l.Details.Contains(email)));
    }

    [Fact(DisplayName = "auditoria: forgot de e-mail inexistente não grava nada")]
    public async Task Audit_UnknownEmail_ShouldNotWriteLog()
    {
        var before = await WithDbAsync(db => db.AuditLogs.CountAsync());

        await ForgotAsync(NewEmail("ghostaud"));
        await Mail.DrainAsync(_factory.Services);

        (await WithDbAsync(db => db.AuditLogs.CountAsync())).Should().Be(before);
    }

    // ── Logs ──────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "logs: código e hash nunca aparecem nos logs da aplicação (forgot, reset errado e reset certo)")]
    public async Task Logs_ShouldNeverContainCodeOrHash()
    {
        var email = await RegisterUserAsync("log");
        var code = await ForgotAndGetCodeAsync(email);
        await ResetAsync(email, WrongCodeFor(code));
        await ResetAsync(email, code);
        await Mail.DrainAsync(_factory.Services);

        var hash = await WithDbAsync(async db =>
        {
            var userId = await db.Users.Where(u => u.Email == email).Select(u => u.Id).SingleAsync();
            return await db.Set<UserToken>().Where(t => t.UserId == userId && t.Purpose == UserTokenPurpose.PasswordReset)
                .Select(t => t.TokenHash).SingleAsync();
        });

        _logs.AllText.Should().NotBeNullOrWhiteSpace();
        _logs.AllText.Should().NotContain(code).And.NotContain(hash);
    }
}
