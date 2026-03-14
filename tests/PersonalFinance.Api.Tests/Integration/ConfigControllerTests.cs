using FluentAssertions;
using Microsoft.OpenApi.Validations;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace PersonalFinance.Api.Tests.Integration;

public class ConfigControllerTests : ApiIntegrationTestBase
{
    public ConfigControllerTests(TestWebApplicationFactory factory) : base(factory) { }

    // ── GET — sem autenticação ─────────────────────────────────────────────────

    [Fact(DisplayName = "GET /config/payment-statuses sem token deve retornar 401")]
    public async Task GetPaymentStatuses_WithoutToken_ShouldReturn401()
    {
        var r = await Client.GetAsync("/api/v1/config/payment-statuses");
        r.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "GET /config/source-types sem token deve retornar 401")]
    public async Task GetSourceTypes_WithoutToken_ShouldReturn401()
    {
        var r = await Client.GetAsync("/api/v1/config/source-types");
        r.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "GET /config/fortnight-types sem token deve retornar 401")]
    public async Task GetFortnightTypes_WithoutToken_ShouldReturn401()
    {
        var r = await Client.GetAsync("/api/v1/config/fortnight-types");
        r.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── GET — autenticado (usuário comum) ─────────────────────────────────────

    [Fact(DisplayName = "GET /config/payment-statuses deve retornar 200 com os 4 seeds")]
    public async Task GetPaymentStatuses_Authenticated_ShouldReturnSeeds()
    {
        var (client, _) = await GetAuthenticatedClientAsync();

        var r    = await client.GetAsync("/api/v1/config/payment-statuses");
        var body = await r.Content.ReadFromJsonAsync<JsonElement>();

        r.StatusCode.Should().Be(HttpStatusCode.OK);
        body.GetArrayLength().Should().BeGreaterThanOrEqualTo(4);
    }

    [Fact(DisplayName = "GET /config/source-types deve retornar 200 com os 2 seeds")]
    public async Task GetSourceTypes_Authenticated_ShouldReturnSeeds()
    {
        var (client, _) = await GetAuthenticatedClientAsync();

        var r    = await client.GetAsync("/api/v1/config/source-types");
        var body = await r.Content.ReadFromJsonAsync<JsonElement>();

        r.StatusCode.Should().Be(HttpStatusCode.OK);
        body.GetArrayLength().Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact(DisplayName = "GET /config/fortnight-types deve retornar 200 com os 2 seeds")]
    public async Task GetFortnightTypes_Authenticated_ShouldReturnSeeds()
    {
        var (client, _) = await GetAdminAuthenticatedClientAsync();

        await client.PostAsJsonAsync("/api/v1/config/fortnight-types",
            new { name = "First" });

        await client.PostAsJsonAsync("/api/v1/config/fortnight-types",
            new { name = "Second" });

        var r    = await client.GetAsync("/api/v1/config/fortnight-types");
        var body = await r.Content.ReadFromJsonAsync<JsonElement>();

        r.StatusCode.Should().Be(HttpStatusCode.OK);
        body.GetArrayLength().Should().BeGreaterThanOrEqualTo(2);
    }

    // ── POST — usuário comum deve receber 403 ─────────────────────────────────

    [Fact(DisplayName = "POST /config/payment-statuses por usuário comum deve retornar 403")]
    public async Task CreatePaymentStatus_NonAdmin_ShouldReturn403()
    {
        var (client, _) = await GetAuthenticatedClientAsync();

        var r = await client.PostAsJsonAsync("/api/v1/config/payment-statuses", new
        { name = "Parcelado", description = "Parcelas mensais" });

        r.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "POST /config/source-types por usuário comum deve retornar 403")]
    public async Task CreateSourceType_NonAdmin_ShouldReturn403()
    {
        var (client, _) = await GetAuthenticatedClientAsync();

        var r = await client.PostAsJsonAsync("/api/v1/config/source-types",
            new { name = "Empresarial" });

        r.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "POST /config/fortnight-types por usuário comum deve retornar 403")]
    public async Task CreateFortnightType_NonAdmin_ShouldReturn403()
    {
        var (client, _) = await GetAuthenticatedClientAsync();

        var r = await client.PostAsJsonAsync("/api/v1/config/fortnight-types",
            new { name = "Third" });

        r.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── POST — seeds retornam IsSystemSeed=true ───────────────────────────────

    [Fact(DisplayName = "Seeds na listagem devem ter IsSystemSeed=true")]
    public async Task GetPaymentStatuses_SeedItems_ShouldHaveIsSystemSeedTrue()
    {
        var (client, _) = await GetAuthenticatedClientAsync();

        var r    = await client.GetAsync("/api/v1/config/payment-statuses");
        var body = await r.Content.ReadFromJsonAsync<JsonElement>();

        var items = body.EnumerateArray().ToList();
        var seeds = items.Where(x => x.GetProperty("id").GetInt32() <= 4).ToList();
        seeds.Should().AllSatisfy(x =>
            x.GetProperty("isSystemSeed").GetBoolean().Should().BeTrue());
    }
}
