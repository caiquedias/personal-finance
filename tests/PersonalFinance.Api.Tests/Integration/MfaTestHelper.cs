using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OtpNet;
using PersonalFinance.Domain.Entities.Auth;
using PersonalFinance.Infrastructure.Persistence.Context;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace PersonalFinance.Api.Tests.Integration;

/// <summary>
/// Helpers compartilhados dos testes de integração de MFA (#393): cadastro, setup/enable via API,
/// cálculo de códigos TOTP (Otp.NET) e leitura direta do banco InMemory.
/// Observação de anti-replay: o código usado no /enable consome o step atual; os códigos usados
/// depois (disable/verify) são calculados para now+30s (step seguinte, dentro da janela ±1).
/// </summary>
public static class MfaTestHelper
{
    public const string Password = "Senha@Teste123";
    public const string LoginPath = "/api/v1/auth/login";
    public const string SetupPath = "/api/v1/auth/mfa/setup";
    public const string EnablePath = "/api/v1/auth/mfa/enable";
    public const string DisablePath = "/api/v1/auth/mfa/disable";
    public const string VerifyPath = "/api/v1/auth/mfa/verify";

    /// <summary>Código TOTP para o instante now + offset (segundos).</summary>
    public static string ComputeCode(string base32Secret, int offsetSeconds = 0) =>
        new Totp(Base32Encoding.ToBytes(base32Secret)).ComputeTotp(DateTime.UtcNow.AddSeconds(offsetSeconds));

    public static async Task<string> RegisterUserAsync(HttpClient client)
    {
        var email = $"mfa_{Guid.NewGuid():N}@monkeybomb.com";
        var response = await client.PostAsJsonAsync("/api/v1/auth/register",
            new { name = "Mfa User", email, password = Password });
        response.EnsureSuccessStatusCode();
        return email;
    }

    public static HttpClient WithBearer(WebApplicationFactory<Program> factory, string token)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    /// <summary>Cadastra, loga (token completo — o usuário ainda não tem MFA), faz setup e enable.</summary>
    public static async Task<MfaUserContext> CreateMfaUserAsync(WebApplicationFactory<Program> factory)
    {
        var anonymous = factory.CreateClient();
        var email = await RegisterUserAsync(anonymous);

        var login = await ReadJsonAsync(await anonymous.PostAsJsonAsync(LoginPath, new { email, password = Password }));
        var token = login.GetProperty("token").GetString()!;
        var authed = WithBearer(factory, token);

        var setup = await ReadJsonAsync(await authed.PostAsync(SetupPath, null));
        var secret = setup.GetProperty("secret").GetString()!;

        var enableCode = ComputeCode(secret);
        var enableResponse = await authed.PostAsJsonAsync(EnablePath, new { code = enableCode });
        enableResponse.EnsureSuccessStatusCode();
        var enable = await ReadJsonAsync(enableResponse);
        var recoveryCodes = enable.GetProperty("recoveryCodes").EnumerateArray().Select(e => e.GetString()!).ToList();

        var userId = await WithDbAsync(factory, db =>
            db.Users.Where(u => u.Email == email).Select(u => u.Id).SingleAsync());

        return new MfaUserContext(userId, email, Password, secret, recoveryCodes, enableCode);
    }

    /// <summary>Login com a senha correta de um usuário com MFA (Enforce=true) — devolve o MfaToken.</summary>
    public static async Task<string> GetChallengeTokenAsync(WebApplicationFactory<Program> factory, MfaUserContext user)
    {
        var response = await factory.CreateClient().PostAsJsonAsync(LoginPath,
            new { email = user.Email, password = user.Password });
        response.EnsureSuccessStatusCode();
        var body = await ReadJsonAsync(response);
        return body.GetProperty("mfaToken").GetString()!;
    }

    public static async Task<T> WithDbAsync<T>(WebApplicationFactory<Program> factory, Func<AppDbContext, Task<T>> action)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await action(db);
    }

    public static Task<User> GetUserAsync(WebApplicationFactory<Program> factory, Guid userId) =>
        WithDbAsync(factory, db => db.Users.AsNoTracking().SingleAsync(u => u.Id == userId));
}
