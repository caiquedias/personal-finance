using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using System.Net;
using System.Net.Http.Json;
using Xunit;
using static PersonalFinance.Api.Tests.Integration.MfaTestHelper;

namespace PersonalFinance.Api.Tests.Integration;

/// <summary>
/// Policy de rate limit "mfa-verify" (#393) em POST /auth/mfa/verify, por IP.
/// Config: RateLimiting:MfaVerify:PermitLimit / WindowSeconds. TestServer sem RemoteIpAddress → "unknown".
/// </summary>
public class MfaVerifyRateLimitingTests : IDisposable
{
    private readonly TestWebApplicationFactory _baseFactory = new();
    private readonly WebApplicationFactory<Program> _factory;

    public MfaVerifyRateLimitingTests()
    {
        var settings = new Dictionary<string, string?>
        {
            ["Auth:Mfa:Enforce"] = "true",
            ["RateLimiting:MfaVerify:PermitLimit"] = "2",
            ["RateLimiting:MfaVerify:WindowSeconds"] = "60"
        };
        _factory = _baseFactory.WithWebHostBuilder(b =>
        {
            foreach (var (key, value) in settings) b.UseSetting(key, value);
            b.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(settings));
        });
    }

    [Fact(DisplayName = "Verify deve retornar 429 JSON com Retry-After ao estourar o limite da policy mfa-verify")]
    public async Task Verify_ExceedingPermitLimit_ShouldReturn429()
    {
        var mfa = await CreateMfaUserAsync(_factory);
        var client = WithBearer(_factory, await GetChallengeTokenAsync(_factory, mfa));

        (await client.PostAsJsonAsync(VerifyPath, new { code = "000000" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.PostAsJsonAsync(VerifyPath, new { code = "000000" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var limited = await client.PostAsJsonAsync(VerifyPath, new { code = "000000" });

        limited.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        limited.Headers.RetryAfter.Should().NotBeNull();
        limited.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
        var body = await ReadJsonAsync(limited);
        body.GetProperty("status").GetInt32().Should().Be(429);
        body.GetProperty("message").GetString().Should().MatchRegex(@"Tente novamente em \d+ segundos");
    }

    [Fact(DisplayName = "A policy mfa-verify não deve afetar o endpoint de login")]
    public async Task VerifyLimit_ShouldNotAffectLogin()
    {
        var mfa = await CreateMfaUserAsync(_factory);
        var client = WithBearer(_factory, await GetChallengeTokenAsync(_factory, mfa));
        for (var i = 0; i < 3; i++)
            await client.PostAsJsonAsync(VerifyPath, new { code = "000000" });

        var login = await _factory.CreateClient().PostAsJsonAsync(LoginPath, new { email = mfa.Email, password = mfa.Password });

        login.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    public void Dispose() => _baseFactory.Dispose();
}
