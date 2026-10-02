using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace PersonalFinance.Api.Tests.Integration;

/// <summary>
/// Validação no startup da chave de cifra do secret TOTP (#393): Auth:Mfa:EncryptionKey deve ser
/// Base64 de exatamente 32 bytes. Chave ausente/ inválida falha ao subir (ValidateOnStart), com
/// mensagem clara — independente da flag Enforce.
/// </summary>
public class MfaOptionsStartupValidationTests : IDisposable
{
    private readonly TestWebApplicationFactory _baseFactory = new();

    private static string AllMessages(Exception? ex)
    {
        var parts = new List<string>();
        for (; ex is not null; ex = ex.InnerException) parts.Add(ex.Message);
        return string.Join(" | ", parts);
    }

    private Exception? StartWith(Dictionary<string, string?> settings)
    {
        var factory = _baseFactory.WithWebHostBuilder(b =>
        {
            foreach (var (key, value) in settings)
                b.UseSetting(key, value);
            b.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(settings));
        });

        try { factory.CreateClient(); return null; }
        catch (Exception ex) { return ex; }
    }

    [Theory(DisplayName = "EncryptionKey ausente, não Base64 ou com tamanho diferente de 32 bytes deve impedir o startup")]
    [InlineData("")]
    [InlineData("not-base64!!")]
    [InlineData("c2hvcnQ=")]                                              // 5 bytes
    [InlineData("AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8AAQIDBAUGBw==")] // 40 bytes
    public void InvalidEncryptionKey_ShouldFailStartup(string key)
    {
        var ex = StartWith(new Dictionary<string, string?> { ["Auth:Mfa:EncryptionKey"] = key });

        ex.Should().NotBeNull($"EncryptionKey='{key}' deve falhar ao subir");
        AllMessages(ex).Should().Contain("EncryptionKey");
    }

    [Fact(DisplayName = "EncryptionKey inválida deve falhar o startup mesmo com Enforce=false")]
    public void InvalidEncryptionKey_ShouldFailEvenWhenEnforceIsOff()
    {
        var ex = StartWith(new Dictionary<string, string?>
        {
            ["Auth:Mfa:EncryptionKey"] = "c2hvcnQ=",
            ["Auth:Mfa:Enforce"] = "false"
        });

        ex.Should().NotBeNull();
        AllMessages(ex).Should().Contain("EncryptionKey");
    }

    [Fact(DisplayName = "EncryptionKey válida (Base64 de 32 bytes) deve subir normalmente")]
    public void ValidEncryptionKey_ShouldStart()
    {
        var ex = StartWith(new Dictionary<string, string?>
        {
            ["Auth:Mfa:EncryptionKey"] = "AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8="
        });

        ex.Should().BeNull();
    }

    [Fact(DisplayName = "Mensagem de erro não deve vazar o valor da chave")]
    public void InvalidEncryptionKey_ShouldNotLeakKeyValue()
    {
        const string badKey = "c2hvcnQ=";

        var ex = StartWith(new Dictionary<string, string?> { ["Auth:Mfa:EncryptionKey"] = badKey });

        AllMessages(ex).Should().NotContain(badKey);
    }

    public void Dispose() => _baseFactory.Dispose();
}
