using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace PersonalFinance.Api.Tests.Integration;

/// <summary>
/// Rate limiter por IP da policy "account-recovery" (#404) nos 4 endpoints anônimos: 429 com corpo JSON e
/// Retry-After. Limite baixo (2/janela) só nesta factory; prefixo de partição próprio (não afeta o login).
/// </summary>
public class AccountRecoveryRateLimitingTests : IDisposable
{
    private readonly TestWebApplicationFactory _baseFactory = new();

    private HttpClient CreateLimitedClient()
    {
        var settings = new Dictionary<string, string?>
        {
            ["RateLimiting:AccountRecovery:PermitLimit"] = "2",
            ["RateLimiting:AccountRecovery:WindowSeconds"] = "60"
        };
        var factory = _baseFactory.WithWebHostBuilder(builder =>
        {
            foreach (var (key, value) in settings) builder.UseSetting(key, value);
            builder.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(settings));
        });
        return factory.CreateClient();
    }

    private static object Payload(string path, string email) => path switch
    {
        "/api/v1/auth/password/reset" => new { email, code = "123456", newPassword = "NovaSenha@456" },
        "/api/v1/auth/email/confirm" => new { email, code = "123456" },
        _ => new { email }
    };

    [Theory(DisplayName = "Endpoints de recuperação devem retornar 429 com JSON e Retry-After ao estourar o limite por IP")]
    [InlineData("/api/v1/auth/password/forgot")]
    [InlineData("/api/v1/auth/password/reset")]
    [InlineData("/api/v1/auth/email/confirm")]
    [InlineData("/api/v1/auth/email/resend")]
    public async Task AccountRecoveryEndpoints_ExceedingLimit_ShouldReturn429WithRetryAfter(string path)
    {
        var client = CreateLimitedClient();
        var payload = Payload(path, "rl_ghost@monkeybomb.com");

        (await client.PostAsJsonAsync(path, payload)).StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
        (await client.PostAsJsonAsync(path, payload)).StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);

        var response = await client.PostAsJsonAsync(path, payload);

        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
        response.Headers.RetryAfter.Should().NotBeNull();
        ((int)response.Headers.RetryAfter!.Delta!.Value.TotalSeconds).Should().BeInRange(1, 60);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetInt32().Should().Be(429);
        body.GetProperty("message").GetString().Should().MatchRegex(@"Tente novamente em \d+ segundos");
        body.TryGetProperty("traceId", out _).Should().BeTrue();
    }

    [Fact(DisplayName = "Esgotar a policy de recuperação não deve bloquear o login (partição própria)")]
    public async Task ExhaustedAccountRecovery_ShouldNotAffectLogin()
    {
        var client = CreateLimitedClient();
        var payload = new { email = "rl_ghost2@monkeybomb.com" };
        for (var i = 0; i < 3; i++)
            await client.PostAsJsonAsync("/api/v1/auth/password/forgot", payload);

        var login = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = "rl_ghost2@monkeybomb.com", password = "Errada@123" });

        login.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "A policy de recuperação não deve afetar outros endpoints")]
    public async Task OtherEndpoints_ShouldNotBeRateLimited()
    {
        var client = CreateLimitedClient();
        for (var i = 0; i < 3; i++)
            await client.PostAsJsonAsync("/api/v1/auth/password/forgot", new { email = "rl_ghost3@monkeybomb.com" });

        for (var i = 0; i < 5; i++)
            (await client.GetAsync("/api/v1/config")).StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
    }

    public void Dispose() => _baseFactory.Dispose();
}
