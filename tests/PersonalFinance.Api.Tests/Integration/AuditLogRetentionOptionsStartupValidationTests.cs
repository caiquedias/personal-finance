using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace PersonalFinance.Api.Tests.Integration;

/// <summary>Configuração inválida da retenção do audit log falha no startup (#402), seção AuditLog:Retention.</summary>
public class AuditLogRetentionOptionsStartupValidationTests : IDisposable
{
    private readonly TestWebApplicationFactory _baseFactory = new();

    private static string AllMessages(Exception? ex)
    {
        var parts = new List<string>();
        for (; ex is not null; ex = ex.InnerException) parts.Add(ex.Message);
        return string.Join(" | ", parts);
    }

    private Exception? StartWith(string key, string value)
    {
        var settings = new Dictionary<string, string?> { [key] = value };
        var factory = _baseFactory.WithWebHostBuilder(b =>
        {
            b.UseSetting(key, value);
            b.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(settings));
        });

        try { factory.CreateClient(); return null; }
        catch (Exception ex) { return ex; }
    }

    [Theory(DisplayName = "Options inválidas da retenção devem impedir o startup com mensagem clara")]
    [InlineData("AuditLog:Retention:RetentionDays", "0", "RetentionDays")]
    [InlineData("AuditLog:Retention:RetentionDays", "-5", "RetentionDays")]
    [InlineData("AuditLog:Retention:PurgeIntervalMinutes", "0", "PurgeIntervalMinutes")]
    [InlineData("AuditLog:Retention:BatchSize", "0", "BatchSize")]
    public void InvalidOptions_ShouldFailStartup(string key, string value, string expectedInMessage)
    {
        var ex = StartWith(key, value);

        ex.Should().NotBeNull($"{key}={value} deve falhar ao subir");
        AllMessages(ex).Should().Contain(expectedInMessage);
    }

    [Fact(DisplayName = "Options válidas devem subir normalmente")]
    public void ValidOptions_ShouldStart()
    {
        StartWith("AuditLog:Retention:RetentionDays", "90").Should().BeNull();
    }

    public void Dispose() => _baseFactory.Dispose();
}
