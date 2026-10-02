using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Net;
using Xunit;

namespace PersonalFinance.Api.Tests.Integration;

/// <summary>
/// ForwardedHeaders deve confiar apenas em proxies de redes privadas/loopback (#391, D1).
/// Confiar em qualquer origem permite forjar X-Forwarded-For e burlar o rate limit por IP.
/// </summary>
public class ForwardedHeadersTrustTests : IDisposable
{
    private readonly TestWebApplicationFactory _baseFactory = new();

    /// <summary>
    /// Filtro externo ao pipeline da app: define o RemoteIpAddress (TestServer não tem)
    /// e captura o IP final após o ForwardedHeadersMiddleware ter rodado.
    /// </summary>
    private sealed class RemoteIpCaptureFilter : IStartupFilter
    {
        public IPAddress? FinalIp;

        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (ctx, nxt) =>
            {
                if (ctx.Request.Headers.TryGetValue("X-Test-Remote", out var remote))
                    ctx.Connection.RemoteIpAddress = IPAddress.Parse(remote.ToString());
                await nxt();
                FinalIp = ctx.Connection.RemoteIpAddress;
            });
            next(app);
        };
    }

    private async Task<IPAddress?> SendAsync(string remoteProxy, string forwardedFor)
    {
        var filter = new RemoteIpCaptureFilter();
        var factory = _baseFactory.WithWebHostBuilder(b =>
            b.ConfigureServices(s => s.AddSingleton<IStartupFilter>(filter)));
        var client = factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/config");
        request.Headers.Add("X-Test-Remote", remoteProxy);
        request.Headers.Add("X-Forwarded-For", forwardedFor);
        request.Headers.Add("X-Forwarded-Proto", "https");
        await client.SendAsync(request);

        return filter.FinalIp;
    }

    [Fact(DisplayName = "Proxy público não confiável: X-Forwarded-For deve ser ignorado")]
    public async Task PublicRemote_ForwardedForIsIgnored()
    {
        var ip = await SendAsync("8.8.8.8", "1.2.3.4");

        ip.Should().Be(IPAddress.Parse("8.8.8.8"));
    }

    [Fact(DisplayName = "Proxy em rede privada (10.0.0.0/8): X-Forwarded-For deve ser aplicado")]
    public async Task PrivateRemote_ForwardedForIsApplied()
    {
        var ip = await SendAsync("10.0.0.5", "1.2.3.4");

        ip.Should().Be(IPAddress.Parse("1.2.3.4"));
    }

    [Fact(DisplayName = "Options devem confiar nas redes privadas e limitar a 1 salto")]
    public void Options_ShouldTrustPrivateNetworksAndLimitHops()
    {
        var options = _baseFactory.Services.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;

        options.ForwardLimit.Should().Be(1);
        options.KnownNetworks.Should().Contain(n => n.Prefix.Equals(IPAddress.Parse("10.0.0.0")) && n.PrefixLength == 8);
        options.KnownNetworks.Should().Contain(n => n.Prefix.Equals(IPAddress.Parse("172.16.0.0")) && n.PrefixLength == 12);
        options.KnownNetworks.Should().Contain(n => n.Prefix.Equals(IPAddress.Parse("192.168.0.0")) && n.PrefixLength == 16);
    }

    public void Dispose() => _baseFactory.Dispose();
}
