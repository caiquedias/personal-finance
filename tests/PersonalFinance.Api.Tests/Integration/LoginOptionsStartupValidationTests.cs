using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace PersonalFinance.Api.Tests.Integration;

/// <summary>
/// Configuração inválida de lockout/rate limit deve falhar claramente no startup (#391, D4).
/// Decisão: falha no startup também para RateLimiting:Login (sem fallback silencioso).
/// </summary>
public class LoginOptionsStartupValidationTests : IDisposable
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
        var factory = _baseFactory.WithWebHostBuilder(b =>
        {
            b.UseSetting(key, value);
            b.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(
                new Dictionary<string, string?> { [key] = value }));
        });

        try { factory.CreateClient(); return null; }
        catch (Exception ex) { return ex; }
    }

    [Theory(DisplayName = "Options inválidas devem impedir o startup com mensagem clara")]
    [InlineData("Auth:LoginLockout:MaxFailedAttempts", "0", "MaxFailedAttempts")]
    [InlineData("Auth:LoginLockout:LockoutMinutes", "0", "LockoutMinutes")]
    [InlineData("RateLimiting:Login:PermitLimit", "0", "PermitLimit")]
    [InlineData("RateLimiting:Login:WindowSeconds", "0", "WindowSeconds")]
    public void InvalidOptions_ShouldFailStartup(string key, string value, string expectedInMessage)
    {
        var ex = StartWith(key, value);

        ex.Should().NotBeNull($"{key}={value} deve falhar ao subir");
        AllMessages(ex).Should().Contain(expectedInMessage);
    }

    [Fact(DisplayName = "Options válidas (defaults) devem subir normalmente")]
    public void ValidOptions_ShouldStart()
    {
        var ex = StartWith("Auth:LoginLockout:MaxFailedAttempts", "3");

        ex.Should().BeNull();
    }

    public void Dispose() => _baseFactory.Dispose();
}
