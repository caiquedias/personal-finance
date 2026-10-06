using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace PersonalFinance.Api.Tests.Integration;

/// <summary>
/// Validação no startup do envio de e-mail (#404, C1): com Email:Enabled=true exigem-se Email:Brevo:ApiKey e
/// Email:Brevo:SenderEmail (formato de e-mail); com Enabled=false nada é validado (dev/testes sem chave).
/// Mensagens citam só o NOME da opção, nunca o valor.
/// </summary>
public class EmailOptionsStartupValidationTests : IDisposable
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

    [Theory(DisplayName = "Enabled=true com ApiKey vazia/ausente deve impedir o startup citando ApiKey")]
    [InlineData("")]
    [InlineData("   ")]
    public void Enabled_WithBlankApiKey_ShouldFailStartup(string apiKey)
    {
        var ex = StartWith(new() { ["Email:Enabled"] = "true", ["Email:Brevo:ApiKey"] = apiKey });

        ex.Should().NotBeNull();
        AllMessages(ex).Should().Contain("ApiKey");
    }

    [Theory(DisplayName = "Enabled=true com SenderEmail vazio ou em formato inválido deve impedir o startup citando SenderEmail")]
    [InlineData("")]
    [InlineData("not-an-email")]
    [InlineData("two@@x.com")]
    public void Enabled_WithInvalidSenderEmail_ShouldFailStartup(string senderEmail)
    {
        var ex = StartWith(new() { ["Email:Enabled"] = "true", ["Email:Brevo:SenderEmail"] = senderEmail });

        ex.Should().NotBeNull($"SenderEmail='{senderEmail}' deve falhar ao subir");
        AllMessages(ex).Should().Contain("SenderEmail");
    }

    [Fact(DisplayName = "Mensagem de erro não vaza o valor do SenderEmail nem da ApiKey")]
    public void InvalidEmailOptions_ShouldNotLeakValues()
    {
        const string badSender = "sender-invalido-visivel";
        const string apiKey = "xkeysib-segredo-nao-pode-vazar";

        var ex = StartWith(new()
        {
            ["Email:Enabled"] = "true",
            ["Email:Brevo:ApiKey"] = apiKey,
            ["Email:Brevo:SenderEmail"] = badSender
        });

        ex.Should().NotBeNull();
        AllMessages(ex).Should().NotContain(badSender).And.NotContain(apiKey);
    }

    [Fact(DisplayName = "Enabled=true com ApiKey e SenderEmail válidos deve subir normalmente")]
    public void Enabled_WithValidBrevoOptions_ShouldStart()
    {
        StartWith(new()
        {
            ["Email:Enabled"] = "true",
            ["Email:Brevo:ApiKey"] = "xkeysib-valida",
            ["Email:Brevo:SenderEmail"] = "no-reply@monkeybomb.test"
        }).Should().BeNull();
    }

    [Fact(DisplayName = "Enabled=false NÃO valida Brevo: sobe sem ApiKey e sem SenderEmail")]
    public void Disabled_WithoutBrevoOptions_ShouldStart()
    {
        StartWith(new()
        {
            ["Email:Enabled"] = "false",
            ["Email:Brevo:ApiKey"] = "",
            ["Email:Brevo:SenderEmail"] = ""
        }).Should().BeNull();
    }

    public void Dispose() => _baseFactory.Dispose();
}
