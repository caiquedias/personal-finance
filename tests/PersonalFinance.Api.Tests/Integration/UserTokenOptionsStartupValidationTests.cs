using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace PersonalFinance.Api.Tests.Integration;

/// <summary>
/// Validação no startup da configuração de reset/verificação (#404): Auth:UserTokens:HmacKey (Base64 de 32 bytes,
/// obrigatória), limites dos tokens, App:FrontendBaseUrl (URL absoluta; https em Production) e a policy
/// RateLimiting:AccountRecovery. Falha ao subir com mensagem clara e SEM vazar o valor da chave.
/// </summary>
public class UserTokenOptionsStartupValidationTests : IDisposable
{
    private const string ValidKey = "ICEiIyQlJicoKSorLC0uLzAxMjM0NTY3ODk6Ozw9Pj8=";

    private readonly TestWebApplicationFactory _baseFactory = new();

    private static string AllMessages(Exception? ex)
    {
        var parts = new List<string>();
        for (; ex is not null; ex = ex.InnerException) parts.Add(ex.Message);
        return string.Join(" | ", parts);
    }

    private Exception? StartWith(Dictionary<string, string?> settings, string? environment = null)
    {
        var factory = _baseFactory.WithWebHostBuilder(b =>
        {
            if (environment is not null) b.UseEnvironment(environment);
            foreach (var (key, value) in settings)
                b.UseSetting(key, value);
            b.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(settings));
        });

        try { factory.CreateClient(); return null; }
        catch (Exception ex) { return ex; }
    }

    private Exception? StartWith(string key, string value, string? environment = null) =>
        StartWith(new Dictionary<string, string?> { [key] = value }, environment);

    // ── HmacKey ───────────────────────────────────────────────────────────────

    [Theory(DisplayName = "HmacKey ausente, não Base64 ou com tamanho diferente de 32 bytes deve impedir o startup")]
    [InlineData("")]
    [InlineData("not-base64!!")]
    [InlineData("c2hvcnQ=")]                                              // 5 bytes
    [InlineData("AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8AAQIDBAUGBw==")] // 40 bytes
    public void InvalidHmacKey_ShouldFailStartup(string key)
    {
        var ex = StartWith("Auth:UserTokens:HmacKey", key);

        ex.Should().NotBeNull($"HmacKey='{key}' deve falhar ao subir");
        AllMessages(ex).Should().Contain("HmacKey");
    }

    [Fact(DisplayName = "HmacKey válida (Base64 de 32 bytes) deve subir normalmente")]
    public void ValidHmacKey_ShouldStart()
    {
        StartWith("Auth:UserTokens:HmacKey", ValidKey).Should().BeNull();
    }

    [Fact(DisplayName = "Mensagem de erro não deve vazar o valor da chave")]
    public void InvalidHmacKey_ShouldNotLeakKeyValue()
    {
        const string badKey = "c2hvcnQ=";

        var ex = StartWith("Auth:UserTokens:HmacKey", badKey);

        AllMessages(ex).Should().NotContain(badKey);
    }

    // ── Limites dos tokens ────────────────────────────────────────────────────

    [Theory(DisplayName = "Limites de token inválidos devem impedir o startup com o nome da opção na mensagem")]
    [InlineData("Auth:UserTokens:CodeTtlMinutes", "0", "CodeTtlMinutes")]
    [InlineData("Auth:UserTokens:MaxAttempts", "0", "MaxAttempts")]
    [InlineData("Auth:UserTokens:ResendCooldownSeconds", "-1", "ResendCooldownSeconds")]
    public void InvalidTokenLimits_ShouldFailStartup(string key, string value, string expectedInMessage)
    {
        var ex = StartWith(key, value);

        ex.Should().NotBeNull($"{key}={value} deve falhar ao subir");
        AllMessages(ex).Should().Contain(expectedInMessage);
    }

    [Fact(DisplayName = "Limites de token válidos devem subir normalmente")]
    public void ValidTokenLimits_ShouldStart()
    {
        StartWith(new Dictionary<string, string?>
        {
            ["Auth:UserTokens:CodeTtlMinutes"] = "15",
            ["Auth:UserTokens:MaxAttempts"] = "5",
            ["Auth:UserTokens:ResendCooldownSeconds"] = "30"
        }).Should().BeNull();
    }

    // ── App:FrontendBaseUrl ───────────────────────────────────────────────────

    [Theory(DisplayName = "FrontendBaseUrl vazia, relativa ou com esquema não http(s) deve impedir o startup")]
    [InlineData("")]
    [InlineData("not-a-url")]
    [InlineData("/relative/path")]
    [InlineData("ftp://app.example.com")]
    public void InvalidFrontendBaseUrl_ShouldFailStartup(string url)
    {
        var ex = StartWith("App:FrontendBaseUrl", url);

        ex.Should().NotBeNull($"FrontendBaseUrl='{url}' deve falhar ao subir");
        AllMessages(ex).Should().Contain("FrontendBaseUrl");
    }

    [Fact(DisplayName = "FrontendBaseUrl https válida (com ou sem barra final) deve subir normalmente")]
    public void ValidFrontendBaseUrl_ShouldStart()
    {
        StartWith("App:FrontendBaseUrl", "https://app.example.com").Should().BeNull();
        StartWith("App:FrontendBaseUrl", "https://app.example.com/").Should().BeNull();
    }

    [Fact(DisplayName = "FrontendBaseUrl http é aceita em Development (localhost)")]
    public void HttpFrontendBaseUrl_InDevelopment_ShouldStart()
    {
        StartWith("App:FrontendBaseUrl", "http://localhost:4200", environment: "Development").Should().BeNull();
    }

    [Fact(DisplayName = "FrontendBaseUrl http deve impedir o startup em Production (https obrigatório)")]
    public void HttpFrontendBaseUrl_InProduction_ShouldFailStartup()
    {
        var ex = StartWith("App:FrontendBaseUrl", "http://app.example.com", environment: "Production");

        ex.Should().NotBeNull();
        AllMessages(ex).Should().Contain("FrontendBaseUrl");
    }

    [Fact(DisplayName = "FrontendBaseUrl https deve subir em Production")]
    public void HttpsFrontendBaseUrl_InProduction_ShouldStart()
    {
        StartWith("App:FrontendBaseUrl", "https://app.example.com", environment: "Production").Should().BeNull();
    }

    // ── RateLimiting:AccountRecovery ──────────────────────────────────────────

    [Theory(DisplayName = "Rate limit de recuperação inválido deve impedir o startup (sem fallback silencioso)")]
    [InlineData("RateLimiting:AccountRecovery:PermitLimit", "0", "PermitLimit")]
    [InlineData("RateLimiting:AccountRecovery:WindowSeconds", "0", "WindowSeconds")]
    public void InvalidAccountRecoveryRateLimit_ShouldFailStartup(string key, string value, string expectedInMessage)
    {
        var ex = StartWith(key, value);

        ex.Should().NotBeNull($"{key}={value} deve falhar ao subir");
        AllMessages(ex).Should().Contain(expectedInMessage);
    }

    public void Dispose() => _baseFactory.Dispose();
}
