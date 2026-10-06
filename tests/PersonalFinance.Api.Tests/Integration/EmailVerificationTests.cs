using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PersonalFinance.Domain.Entities.Auth;
using PersonalFinance.Domain.Enums;
using PersonalFinance.Domain.Interfaces.Services;
using PersonalFinance.Infrastructure.Persistence.Context;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;
using static PersonalFinance.Api.Tests.Integration.MfaTestHelper;

namespace PersonalFinance.Api.Tests.Integration;

/// <summary>
/// Verificação de e-mail ponta a ponta (#404): register 202 genérico + código por e-mail, confirm/resend anônimos
/// com resposta genérica, tentativas/uso único e enforcement opcional no login (Auth:EmailVerification:Enforce).
/// </summary>
public class EmailVerificationTests : IDisposable
{
    private const string RegisterPath = "/api/v1/auth/register";
    private const string LoginPath = "/api/v1/auth/login";
    private const string ConfirmPath = "/api/v1/auth/email/confirm";
    private const string ResendPath = "/api/v1/auth/email/resend";
    private const string ForgotPath = "/api/v1/auth/password/forgot";
    private const string ResetPath = "/api/v1/auth/password/reset";
    private const string Password = "Senha@Verif123";
    private const string InvalidCodeMessage = "Código inválido ou expirado.";
    private const string Credentials = "Credenciais inválidas.";

    private readonly TestWebApplicationFactory _baseFactory = new();
    private readonly WebApplicationFactory<Program> _enforced;
    private readonly HttpClient _client;

    public EmailVerificationTests()
    {
        var settings = new Dictionary<string, string?> { ["Auth:EmailVerification:Enforce"] = "true" };
        _enforced = _baseFactory.WithWebHostBuilder(b =>
        {
            foreach (var (key, value) in settings) b.UseSetting(key, value);
            b.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(settings));
        });
        _client = _baseFactory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _baseFactory.Dispose();
    }

    private Fakes.FakeEmailSender Mail => _baseFactory.EmailSender;

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string NewEmail(string prefix)
    {
        var letters = new string(Guid.NewGuid().ToByteArray().Select(b => (char)('a' + b % 26)).ToArray());
        return $"{prefix}{letters}@monkeybomb.com";
    }

    private async Task<string> RegisterAsync(string prefix, HttpClient? client = null)
    {
        var email = NewEmail(prefix);
        var response = await (client ?? _client).PostAsJsonAsync(RegisterPath, new { name = "Conta Verif", email, password = Password });
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        return email;
    }

    private Task<HttpResponseMessage> ConfirmAsync(string email, string code, HttpClient? client = null) =>
        (client ?? _client).PostAsJsonAsync(ConfirmPath, new { email, code });

    private Task<HttpResponseMessage> ResendAsync(string email) => _client.PostAsJsonAsync(ResendPath, new { email });

    private static string WrongCodeFor(string code) => code == "000000" ? "999999" : "000000";

    private static async Task<string> MessageOf(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("message").GetString()!;
    }

    private Task<T> WithDbAsync<T>(Func<AppDbContext, Task<T>> action) => MfaTestHelper.WithDbAsync(_baseFactory, action);

    /// <summary>Cria um usuário direto no banco (sem register, logo sem token de verificação pendente).</summary>
    private async Task<string> CreateUserWithoutTokenAsync(string prefix)
    {
        var email = NewEmail(prefix);
        using var scope = _baseFactory.Services.CreateScope();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Users.Add(User.Create("Sem Token", email, hasher.Hash(Password)));
        await db.SaveChangesAsync();
        return email;
    }

    // ── Register ──────────────────────────────────────────────────────────────

    [Fact(DisplayName = "register: conta nova recebe e-mail com código de verificação e link /confirm-email sem o código")]
    public async Task Register_NewUser_ShouldSendVerificationEmail()
    {
        var email = await RegisterAsync("rg");

        var code = await Mail.WaitForCodeAsync(email, "/confirm-email");

        code.Should().MatchRegex("^[0-9]{6}$");
        var html = Mail.SentTo(email, "/confirm-email").Last().HtmlBody;
        html.Should().Contain($"{TestWebApplicationFactory.TestFrontendBaseUrl}/confirm-email#email={Uri.EscapeDataString(email)}");
        var user = await WithDbAsync(db => db.Users.AsNoTracking().SingleAsync(u => u.Email == email));
        user.IsEmailConfirmed.Should().BeFalse();
    }

    [Fact(DisplayName = "register: e-mail duplicado não dispara novo e-mail de verificação")]
    public async Task Register_DuplicateEmail_ShouldNotSendSecondEmail()
    {
        var email = await RegisterAsync("rgdup");
        await Mail.WaitForCodeAsync(email, "/confirm-email");

        var again = await _client.PostAsJsonAsync(RegisterPath, new { name = "Outro", email, password = "Outra@Senha456" });
        await Mail.DrainAsync(_baseFactory.Services);

        again.StatusCode.Should().Be(HttpStatusCode.Accepted);
        Mail.SentTo(email, "/confirm-email").Should().HaveCount(1);
    }

    // ── POST /email/confirm ───────────────────────────────────────────────────

    [Fact(DisplayName = "confirm: código correto confirma o e-mail (200) e marca o token como usado")]
    public async Task Confirm_WithCorrectCode_ShouldConfirmEmail()
    {
        var email = await RegisterAsync("cf");
        var code = await Mail.WaitForCodeAsync(email, "/confirm-email");

        var response = await ConfirmAsync(email, code);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var (user, token) = await WithDbAsync(async db =>
        {
            var u = await db.Users.AsNoTracking().SingleAsync(x => x.Email == email);
            var t = await db.Set<UserToken>().AsNoTracking()
                .SingleAsync(x => x.UserId == u.Id && x.Purpose == UserTokenPurpose.EmailVerification);
            return (u, t);
        });
        user.IsEmailConfirmed.Should().BeTrue();
        token.UsedAt.Should().NotBeNull();
        token.TokenHash.Should().NotContain(code);
    }

    [Fact(DisplayName = "confirm: reutilizar o código após o sucesso retorna 400")]
    public async Task Confirm_ReusingCode_ShouldReturn400()
    {
        var email = await RegisterAsync("cfre");
        var code = await Mail.WaitForCodeAsync(email, "/confirm-email");
        (await ConfirmAsync(email, code)).StatusCode.Should().Be(HttpStatusCode.OK);

        (await ConfirmAsync(email, code)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "confirm: código errado retorna 400 (nunca 401) com a mensagem genérica")]
    public async Task Confirm_WrongCode_ShouldReturn400WithGenericMessage()
    {
        var email = await RegisterAsync("cfwr");
        var code = await Mail.WaitForCodeAsync(email, "/confirm-email");

        var response = await ConfirmAsync(email, WrongCodeFor(code));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await MessageOf(response)).Should().Be(InvalidCodeMessage);
    }

    [Fact(DisplayName = "confirm: 3 códigos errados invalidam o token — o código certo depois é recusado")]
    public async Task Confirm_ThreeWrongCodes_ShouldInvalidateToken()
    {
        var email = await RegisterAsync("cf3x");
        var code = await Mail.WaitForCodeAsync(email, "/confirm-email");

        for (var i = 0; i < 3; i++)
            (await ConfirmAsync(email, WrongCodeFor(code))).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await ConfirmAsync(email, code)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var user = await WithDbAsync(db => db.Users.AsNoTracking().SingleAsync(u => u.Email == email));
        user.IsEmailConfirmed.Should().BeFalse();
    }

    [Fact(DisplayName = "confirm: e-mail inexistente responde 400 idêntico ao código errado de conta existente")]
    public async Task Confirm_UnknownEmail_ShouldBeIndistinguishableFromWrongCode()
    {
        var email = await RegisterAsync("cfid");
        var code = await Mail.WaitForCodeAsync(email, "/confirm-email");

        var wrong = await ConfirmAsync(email, WrongCodeFor(code));
        var unknown = await ConfirmAsync(NewEmail("nobody"), "123456");

        unknown.StatusCode.Should().Be(wrong.StatusCode).And.Be(HttpStatusCode.BadRequest);
        (await MessageOf(unknown)).Should().Be(await MessageOf(wrong));
    }

    [Fact(DisplayName = "confirm: código de reset de senha não serve para confirmar e-mail (propósito diferente)")]
    public async Task Confirm_WithResetCode_ShouldReturn400()
    {
        var email = await RegisterAsync("cfprp");
        await _client.PostAsJsonAsync(ForgotPath, new { email });
        var resetCode = await Mail.WaitForCodeAsync(email, "/reset-password");

        var response = await ConfirmAsync(email, resetCode);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory(DisplayName = "confirm: payload inválido retorna 400")]
    [InlineData("nao_e_email", "123456")]
    [InlineData("a@x.com", "12345")]
    [InlineData("a@x.com", "1234567")]
    [InlineData("a@x.com", "12345a")]
    public async Task Confirm_InvalidPayload_ShouldReturn400(string email, string code)
    {
        (await ConfirmAsync(email, code)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── POST /email/resend ────────────────────────────────────────────────────

    [Fact(DisplayName = "resend: usuário sem token pendente recebe novo e-mail e consegue confirmar com ele")]
    public async Task Resend_ExistingUnverifiedUser_ShouldSendNewCode()
    {
        var email = await CreateUserWithoutTokenAsync("rsd");

        var response = await ResendAsync(email);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var code = await Mail.WaitForCodeAsync(email, "/confirm-email");
        (await ConfirmAsync(email, code)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "resend: e-mail inexistente responde idêntico ao existente (status e corpo) e não envia e-mail")]
    public async Task Resend_UnknownEmail_ShouldBeIndistinguishableAndSendNothing()
    {
        var existing = await CreateUserWithoutTokenAsync("rsdex");
        var unknown = NewEmail("ghost");

        var forExisting = await ResendAsync(existing);
        var forUnknown = await ResendAsync(unknown);
        await Mail.DrainAsync(_baseFactory.Services);

        forUnknown.StatusCode.Should().Be(forExisting.StatusCode).And.Be(HttpStatusCode.Accepted);
        (await forUnknown.Content.ReadAsStringAsync()).Should().Be(await forExisting.Content.ReadAsStringAsync());
        Mail.SentTo(unknown, "/confirm-email").Should().BeEmpty();
    }

    [Fact(DisplayName = "resend: dentro do cooldown responde igual mas não envia segundo e-mail")]
    public async Task Resend_WithinCooldown_ShouldNotSendSecondEmail()
    {
        var email = await RegisterAsync("rsdcd"); // register já emitiu o código (cooldown de 60s corre)
        await Mail.WaitForCodeAsync(email, "/confirm-email");

        var response = await ResendAsync(email);
        await Mail.DrainAsync(_baseFactory.Services);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        Mail.SentTo(email, "/confirm-email").Should().HaveCount(1);
    }

    [Fact(DisplayName = "resend: e-mail já confirmado responde 202 e não envia e-mail")]
    public async Task Resend_AlreadyConfirmed_ShouldSendNothing()
    {
        var email = await CreateUserWithoutTokenAsync("rsdok");
        await WithDbAsync(async db =>
        {
            (await db.Users.SingleAsync(u => u.Email == email)).ConfirmEmail(DateTime.UtcNow);
            await db.SaveChangesAsync();
            return 0;
        });

        var response = await ResendAsync(email);
        await Mail.DrainAsync(_baseFactory.Services);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        Mail.SentTo(email, "/confirm-email").Should().BeEmpty();
    }

    [Theory(DisplayName = "resend: e-mail ausente ou inválido retorna 400")]
    [InlineData("")]
    [InlineData("nao_e_email")]
    public async Task Resend_InvalidEmail_ShouldReturn400(string email)
    {
        (await ResendAsync(email)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── Enforcement no login ──────────────────────────────────────────────────

    [Fact(DisplayName = "Enforce=false (default): usuário não verificado loga normalmente")]
    public async Task Login_EnforceOff_UnverifiedUser_ShouldLogin()
    {
        var email = await RegisterAsync("enoff");

        (await _client.PostAsJsonAsync(LoginPath, new { email, password = Password })).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "Enforce=true: senha correta com e-mail não verificado retorna 400 'Credenciais inválidas.'")]
    public async Task Login_EnforceOn_UnverifiedUser_ShouldBeRejected()
    {
        var client = _enforced.CreateClient();
        var email = await RegisterAsync("enon", client);

        var response = await client.PostAsJsonAsync(LoginPath, new { email, password = Password });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await MessageOf(response)).Should().Be(Credentials);
    }

    [Fact(DisplayName = "Enforce=true: a recusa por e-mail não verificado é indistinguível de senha errada (mesma mensagem)")]
    public async Task Login_EnforceOn_UnverifiedMessage_ShouldMatchWrongPasswordMessage()
    {
        var client = _enforced.CreateClient();
        var email = await RegisterAsync("enmsg", client);

        var unverified = await client.PostAsJsonAsync(LoginPath, new { email, password = Password });
        var wrong = await client.PostAsJsonAsync(LoginPath, new { email, password = "SenhaErrada@1" });

        unverified.StatusCode.Should().Be(wrong.StatusCode);
        (await MessageOf(unverified)).Should().Be(await MessageOf(wrong));
    }

    [Fact(DisplayName = "Enforce=true: depois de confirmar o e-mail o login funciona")]
    public async Task Login_EnforceOn_AfterConfirm_ShouldLogin()
    {
        var client = _enforced.CreateClient();
        var email = await RegisterAsync("enok", client);
        var code = await Mail.WaitForCodeAsync(email, "/confirm-email");
        (await ConfirmAsync(email, code, client)).StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await client.PostAsJsonAsync(LoginPath, new { email, password = Password });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJsonAsync(response)).GetProperty("token").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact(DisplayName = "Enforce=true: concluir o reset de senha confirma o e-mail e libera o login")]
    public async Task Login_EnforceOn_AfterPasswordReset_ShouldLogin()
    {
        var client = _enforced.CreateClient();
        var email = await RegisterAsync("enrst", client);
        (await client.PostAsJsonAsync(ForgotPath, new { email })).StatusCode.Should().Be(HttpStatusCode.Accepted);
        var code = await Mail.WaitForCodeAsync(email, "/reset-password");
        const string newPassword = "NovaSenha@456";

        (await client.PostAsJsonAsync(ResetPath, new { email, code, newPassword })).StatusCode.Should().Be(HttpStatusCode.OK);

        (await client.PostAsJsonAsync(LoginPath, new { email, password = newPassword })).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "Enforce=true: admin semeado (e-mail confirmado) continua logando")]
    public async Task Login_EnforceOn_SeededAdmin_ShouldLogin()
    {
        var response = await _enforced.CreateClient().PostAsJsonAsync(LoginPath,
            new { email = "caique_dias@outlook.com", password = "Arkham@01" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "Enforce=true: usuário criado pelo admin já nasce verificado e loga")]
    public async Task Login_EnforceOn_AdminCreatedUser_ShouldLogin()
    {
        var client = _enforced.CreateClient();
        var adminLogin = await ReadJsonAsync(await client.PostAsJsonAsync(LoginPath,
            new { email = "caique_dias@outlook.com", password = "Arkham@01" }));
        var admin = WithBearer(_enforced, adminLogin.GetProperty("token").GetString()!);
        var email = NewEmail("byadmin");
        var created = await admin.PostAsJsonAsync("/api/v1/admin/users", new { name = "Criado Admin", email, password = Password });
        created.StatusCode.Should().Be(HttpStatusCode.Created);

        var response = await client.PostAsJsonAsync(LoginPath, new { email, password = Password });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
