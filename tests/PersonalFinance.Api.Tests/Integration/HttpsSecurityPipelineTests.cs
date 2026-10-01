using Microsoft.AspNetCore.Hosting;
using Xunit;
using FluentAssertions;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using System.Net;

namespace PersonalFinance.Api.Tests.Integration;

/// <summary>
/// Valida HTTPS redirection, HSTS e ForwardedHeaders no pipeline (#390).
/// </summary>
public class HttpsSecurityPipelineTests : IDisposable
{
    private const string Path = "/api/v1/config";
    private const string HstsHeader = "Strict-Transport-Security";

    private readonly TestWebApplicationFactory _baseFactory = new();

    private HttpClient CreateClient(string environment, bool configureHttpsPort)
    {
        var factory = _baseFactory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
            if (configureHttpsPort)
                builder.ConfigureServices(s => s.Configure<HttpsRedirectionOptions>(o => o.HttpsPort = 443));
        });

        return factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    [Fact]
    public async Task Production_WithForwardedProtoHttps_EmitsHstsHeader()
    {
        var client = CreateClient("Production", configureHttpsPort: false);
        var request = new HttpRequestMessage(HttpMethod.Get, Path);
        request.Headers.Add("X-Forwarded-Proto", "https");

        var response = await client.SendAsync(request);

        response.Headers.Contains(HstsHeader).Should().BeTrue();
    }

    [Fact]
    public async Task Production_HttpWithoutForwardedProto_RedirectsToHttps()
    {
        var client = CreateClient("Production", configureHttpsPort: true);

        var response = await client.GetAsync(Path);

        response.StatusCode.Should().Be(HttpStatusCode.TemporaryRedirect);
        response.Headers.Location!.Scheme.Should().Be("https");
    }

    [Fact]
    public async Task Production_WithForwardedProtoHttps_DoesNotRedirect()
    {
        var client = CreateClient("Production", configureHttpsPort: true);
        var request = new HttpRequestMessage(HttpMethod.Get, Path);
        request.Headers.Add("X-Forwarded-Proto", "https");

        var response = await client.SendAsync(request);

        response.StatusCode.Should().NotBe(HttpStatusCode.TemporaryRedirect);
        response.Headers.Location.Should().BeNull();
    }

    [Fact]
    public async Task Development_WithForwardedProtoHttps_DoesNotEmitHstsHeader()
    {
        var client = CreateClient("Development", configureHttpsPort: false);
        var request = new HttpRequestMessage(HttpMethod.Get, Path);
        request.Headers.Add("X-Forwarded-Proto", "https");

        var response = await client.SendAsync(request);

        response.Headers.Contains(HstsHeader).Should().BeFalse();
    }

    public void Dispose() => _baseFactory.Dispose();
}
