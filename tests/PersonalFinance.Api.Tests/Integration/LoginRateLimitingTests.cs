using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace PersonalFinance.Api.Tests.Integration;

/// <summary>
/// Valida o rate limiter por IP do endpoint de login (#391).
/// Usa factory derivada com limite baixo (2/janela); o TestServer não tem
/// RemoteIpAddress, então também cobre o fallback de IP nulo ("unknown").
/// </summary>
public class LoginRateLimitingTests : IDisposable
{
    private const string LoginPath = "/api/v1/auth/login";
    private readonly TestWebApplicationFactory _baseFactory = new();

    private HttpClient CreateLimitedClient()
    {
        var factory = _baseFactory.WithWebHostBuilder(builder =>
        {
            // Program.cs lê a config de forma síncrona; UseSetting + in-memory cobrem as duas vias
            builder.UseSetting("RateLimiting:Login:PermitLimit", "2");
            builder.UseSetting("RateLimiting:Login:WindowSeconds", "60");
            builder.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["RateLimiting:Login:PermitLimit"] = "2",
                    ["RateLimiting:Login:WindowSeconds"] = "60"
                }));
        });
        return factory.CreateClient();
    }

    [Fact(DisplayName = "Login deve retornar 429 com corpo JSON ao estourar o limite por IP")]
    public async Task Login_ExceedingPermitLimit_ShouldReturn429WithJsonBody()
    {
        var client = CreateLimitedClient();
        var payload = new { email = $"rl_{Guid.NewGuid():N}@x.com", password = "Errada" };

        // IP nulo no TestServer: as primeiras requisições passam (400), nunca 500
        (await client.PostAsJsonAsync(LoginPath, payload)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.PostAsJsonAsync(LoginPath, payload)).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var r = await client.PostAsJsonAsync(LoginPath, payload);

        r.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        r.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
        var body = await r.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetInt32().Should().Be(429);
        body.TryGetProperty("error", out _).Should().BeTrue();
        body.TryGetProperty("message", out _).Should().BeTrue();
        body.TryGetProperty("traceId", out _).Should().BeTrue();
    }

    [Fact(DisplayName = "429 deve enviar Retry-After e mensagem com os segundos de espera")]
    public async Task Login_ExceedingPermitLimit_ShouldSendRetryAfterAndSecondsInMessage()
    {
        var client = CreateLimitedClient();
        var payload = new { email = $"rl_{Guid.NewGuid():N}@x.com", password = "Errada" };
        await client.PostAsJsonAsync(LoginPath, payload);
        await client.PostAsJsonAsync(LoginPath, payload);

        var r = await client.PostAsJsonAsync(LoginPath, payload);

        r.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        r.Headers.RetryAfter.Should().NotBeNull();
        var seconds = (int)r.Headers.RetryAfter!.Delta!.Value.TotalSeconds;
        seconds.Should().BeInRange(1, 60);
        var body = await r.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("message").GetString().Should()
            .MatchRegex(@"Tente novamente em \d+ segundos")
            .And.Contain($"{seconds} segundos");
    }

    [Fact(DisplayName = "Rate limiter de login não deve afetar outros endpoints")]
    public async Task OtherEndpoints_ShouldNotBeRateLimited()
    {
        var client = CreateLimitedClient();

        for (var i = 0; i < 5; i++)
        {
            var r = await client.GetAsync("/api/v1/config");
            r.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
        }
    }

    public void Dispose() => _baseFactory.Dispose();
}
