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

    private Exception? StartWith(string key, string value) =>
        StartWith(new Dictionary<string, string?> { [key] = value });

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

    [Theory(DisplayName = "Options inválidas devem impedir o startup com mensagem clara")]
    [InlineData("Auth:LoginLockout:MaxFailedAttempts", "0", "MaxFailedAttempts")]
    [InlineData("Auth:LoginLockout:LockoutMinutes", "0", "LockoutMinutes")]
    [InlineData("RateLimiting:Login:PermitLimit", "0", "PermitLimit")]
    [InlineData("Auth:LoginLockout:GlobalMaxFailedAttempts", "0", "GlobalMaxFailedAttempts")]
    [InlineData("Auth:LoginLockout:GlobalMaxFailedAttempts", "4", "GlobalMaxFailedAttempts")]
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

    [Fact(DisplayName = "GlobalMaxFailedAttempts igual a MaxFailedAttempts deve subir normalmente (>= é válido)")]
    public void GlobalEqualToMax_ShouldStart()
    {
        var ex = StartWith(new Dictionary<string, string?>
        {
            ["Auth:LoginLockout:MaxFailedAttempts"] = "3",
            ["Auth:LoginLockout:GlobalMaxFailedAttempts"] = "3"
        });

        ex.Should().BeNull();
    }

    public void Dispose() => _baseFactory.Dispose();
}
