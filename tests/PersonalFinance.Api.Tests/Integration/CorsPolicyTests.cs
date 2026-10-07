using FluentAssertions;
using Xunit;

namespace PersonalFinance.Api.Tests.Integration;

/// <summary>
/// Valida a política CORS restrita "AllowAngular" via preflight (#399).
/// Asserções sobre headers Access-Control-*, não sobre status.
/// </summary>
public class CorsPolicyTests : IClassFixture<TestWebApplicationFactory>
{
    private const string Path = "/api/v1/config";
    private const string AllowedOrigin = "http://localhost:4200";

    private readonly HttpClient _client;

    public CorsPolicyTests(TestWebApplicationFactory factory) => _client = factory.CreateClient();

    private async Task<HttpResponseMessage> PreflightAsync(string origin, string method, string? headers = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, Path);
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", method);
        if (headers is not null)
            request.Headers.Add("Access-Control-Request-Headers", headers);
        return await _client.SendAsync(request);
    }

    private static string[] Tokens(HttpResponseMessage response, string header) =>
        response.Headers.TryGetValues(header, out var values)
            ? values.SelectMany(v => v.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)).ToArray()
            : [];

    [Theory]
    [InlineData("GET")]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    public async Task Preflight_AllowedMethod_IsListedInAllowMethods(string method)
    {
        var response = await PreflightAsync(AllowedOrigin, method);

        Tokens(response, "Access-Control-Allow-Methods").Should().Contain(method);
    }

    [Fact]
    public async Task Preflight_AuthorizationAndContentType_AreAllowed()
    {
        var response = await PreflightAsync(AllowedOrigin, "POST", "authorization,content-type");

        Tokens(response, "Access-Control-Allow-Headers")
            .Select(h => h.ToLowerInvariant())
            .Should().Contain(["authorization", "content-type"]);
    }

    [Fact]
    public async Task Preflight_DisallowedMethod_IsNotListedInAllowMethods()
    {
        var response = await PreflightAsync(AllowedOrigin, "TRACE");

        Tokens(response, "Access-Control-Allow-Methods").Should().NotContain("TRACE");
    }

    [Fact]
    public async Task Preflight_CustomHeader_IsNotListedInAllowHeaders()
    {
        var response = await PreflightAsync(AllowedOrigin, "GET", "authorization,x-custom-header");

        Tokens(response, "Access-Control-Allow-Headers")
            .Select(h => h.ToLowerInvariant())
            .Should().NotContain("x-custom-header");
    }

    [Fact]
    public async Task Preflight_DisallowedOrigin_HasNoAllowOriginHeader()
    {
        var response = await PreflightAsync("http://evil.com", "GET");

        response.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();
    }
}
