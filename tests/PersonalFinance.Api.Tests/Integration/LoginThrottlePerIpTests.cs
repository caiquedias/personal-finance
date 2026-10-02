using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PersonalFinance.Infrastructure.Persistence.Context;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace PersonalFinance.Api.Tests.Integration;

/// <summary>
/// Lockout por par (conta, IP) + teto global por conta (#391, D6).
/// O IP do cliente é simulado por um IStartupFilter externo (header X-Test-Remote), como em
/// ForwardedHeadersTrustTests. IPs públicos (8.8.8.8, 9.9.9.9) não são proxies confiáveis,
/// logo X-Forwarded-For não interfere.
/// </summary>
public class LoginThrottlePerIpTests : IDisposable
{
    private const string IpA = "8.8.8.8";
    private const string IpB = "9.9.9.9";
    private const string Password = "Senha@123";

    private readonly TestWebApplicationFactory _baseFactory = new();

    private sealed class RemoteIpFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (ctx, nxt) =>
            {
                if (ctx.Request.Headers.TryGetValue("X-Test-Remote", out var remote))
                    ctx.Connection.RemoteIpAddress = IPAddress.Parse(remote.ToString());
                await nxt();
            });
            next(app);
        };
    }

    private WebApplicationFactoryHandle Create(Dictionary<string, string?>? settings = null)
    {
        var factory = _baseFactory.WithWebHostBuilder(b =>
        {
            b.ConfigureServices(s => s.AddSingleton<IStartupFilter>(new RemoteIpFilter()));
            if (settings is null) return;
            foreach (var (key, value) in settings) b.UseSetting(key, value);
            b.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(settings));
        });
        return new WebApplicationFactoryHandle(factory, factory.CreateClient());
    }

    private sealed record WebApplicationFactoryHandle(
        Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> Factory, HttpClient Client);

    private static async Task<string> RegisterAsync(HttpClient client)
    {
        var email = $"thr_{Guid.NewGuid():N}@x.com";
        var r = await client.PostAsJsonAsync("/api/v1/auth/register", new { name = "Thr", email, password = Password });
        r.EnsureSuccessStatusCode();
        return email;
    }

    private static Task<HttpResponseMessage> LoginAsync(HttpClient client, string email, string password, string ip)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
        {
            Content = JsonContent.Create(new { email, password })
        };
        request.Headers.Add("X-Test-Remote", ip);
        return client.SendAsync(request);
    }

    private static async Task FailAsync(HttpClient client, string email, string ip, int times)
    {
        for (var i = 0; i < times; i++)
            await LoginAsync(client, email, "Errada", ip);
    }

    /// <summary>
    /// Lê as linhas de LoginThrottle por reflexão (entidade ainda não existe no Red) e devolve os IPs gravados.
    /// </summary>
    private static async Task<List<string>> ThrottleIpsAsync(WebApplicationFactoryHandle handle)
    {
        var type = Type.GetType("PersonalFinance.Domain.Entities.Auth.LoginThrottle, PersonalFinance.Domain");
        type.Should().NotBeNull("a entidade LoginThrottle deve existir no Domain");

        using var scope = handle.Factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var set = typeof(DbContext).GetMethod(nameof(DbContext.Set), Type.EmptyTypes)!
            .MakeGenericMethod(type!).Invoke(ctx, null)!;
        var items = await Task.Run(() => ((System.Collections.IEnumerable)set).Cast<object>().ToList());
        return items.Select(i => (string)type!.GetProperty("IpAddress")!.GetValue(i)!).ToList();
    }

    [Fact(DisplayName = "5 falhas do mesmo IP bloqueiam o par: senha correta do mesmo IP é recusada")]
    public async Task SameIp_AfterFiveFailures_ShouldRejectCorrectPassword()
    {
        var h = Create();
        var email = await RegisterAsync(h.Client);

        await FailAsync(h.Client, email, IpA, 5);
        var r = await LoginAsync(h.Client, email, Password, IpA);

        r.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await r.Content.ReadAsStringAsync()).Should().Contain("Credenciais inválidas.");
    }

    [Fact(DisplayName = "Par bloqueado em um IP não bloqueia a vítima em outro IP")]
    public async Task OtherIp_WhenPairLocked_ShouldStillLogin()
    {
        var h = Create();
        var email = await RegisterAsync(h.Client);
        await FailAsync(h.Client, email, IpA, 5);

        var fromB = await LoginAsync(h.Client, email, Password, IpB);

        fromB.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "Teto global: falhas de vários IPs bloqueiam a conta para qualquer IP")]
    public async Task GlobalCeiling_ShouldLockAccountAcrossIps()
    {
        var h = Create(new Dictionary<string, string?>
        {
            ["Auth:LoginLockout:MaxFailedAttempts"] = "2",
            ["Auth:LoginLockout:GlobalMaxFailedAttempts"] = "4"
        });
        var email = await RegisterAsync(h.Client);

        await FailAsync(h.Client, email, IpA, 2);        // par A bloqueado (contador global = 2)
        await FailAsync(h.Client, email, IpB, 2);        // par B bloqueado (contador global = 4) -> conta bloqueada
        var fromNewIp = await LoginAsync(h.Client, email, Password, "7.7.7.7");

        fromNewIp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await fromNewIp.Content.ReadAsStringAsync()).Should().Contain("Credenciais inválidas.");
    }

    [Fact(DisplayName = "Abaixo do teto global, pares bloqueados não bloqueiam a conta para um terceiro IP")]
    public async Task BelowGlobalCeiling_ThirdIpShouldStillLogin()
    {
        var h = Create(new Dictionary<string, string?>
        {
            ["Auth:LoginLockout:MaxFailedAttempts"] = "2",
            ["Auth:LoginLockout:GlobalMaxFailedAttempts"] = "4"
        });
        var email = await RegisterAsync(h.Client);

        await FailAsync(h.Client, email, IpA, 2);        // par A bloqueado
        await FailAsync(h.Client, email, IpB, 1);        // contador global = 3 (< 4)
        var fromNewIp = await LoginAsync(h.Client, email, Password, "7.7.7.7");

        fromNewIp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "Sucesso zera os contadores do par e do usuário")]
    public async Task Success_ShouldResetPairAndUserCounters()
    {
        var h = Create();
        var email = await RegisterAsync(h.Client);

        await FailAsync(h.Client, email, IpA, 4);
        (await LoginAsync(h.Client, email, Password, IpA)).StatusCode.Should().Be(HttpStatusCode.OK);
        await FailAsync(h.Client, email, IpA, 4);
        var r = await LoginAsync(h.Client, email, Password, IpA);

        r.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "E-mail inexistente não grava nenhuma linha de par")]
    public async Task UnknownEmail_ShouldNotWriteThrottleRows()
    {
        var h = Create();

        await FailAsync(h.Client, $"ghost_{Guid.NewGuid():N}@x.com", IpA, 6);

        (await ThrottleIpsAsync(h)).Should().BeEmpty();
    }

    [Fact(DisplayName = "Conta existente grava uma única linha por par (upsert), com o IP do cliente")]
    public async Task ExistingAccount_ShouldWriteSingleRowPerPair()
    {
        var h = Create();
        var email = await RegisterAsync(h.Client);

        await FailAsync(h.Client, email, IpA, 3);

        var ips = await ThrottleIpsAsync(h);
        ips.Should().ContainSingle().Which.Should().Be(IpA);
    }

    [Fact(DisplayName = "IP nulo (TestServer) cai no fallback 'unknown' e ainda bloqueia o par após 5 falhas")]
    public async Task NullRemoteIp_ShouldUseUnknownFallback()
    {
        var h = Create();
        var email = await RegisterAsync(h.Client);

        for (var i = 0; i < 5; i++)
            await h.Client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "Errada" });
        var r = await h.Client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = Password });

        r.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ThrottleIpsAsync(h)).Should().ContainSingle().Which.Should().Be("unknown");
    }

    public void Dispose() => _baseFactory.Dispose();
}
