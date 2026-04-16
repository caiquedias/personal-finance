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

    // ── PUT — não-admin deve receber 403 ─────────────────────────────────────

    [Fact(DisplayName = "PUT /config/payment-statuses por usuário comum deve retornar 403")]
    public async Task UpdatePaymentStatus_NonAdmin_ShouldReturn403()
    {
        var (client, _) = await GetAuthenticatedClientAsync();

        var r = await client.PutAsJsonAsync("/api/v1/config/payment-statuses/5",
            new { name = "Parcelado", description = "Desc" });

        r.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "PUT /config/source-types por usuário comum deve retornar 403")]
    public async Task UpdateSourceType_NonAdmin_ShouldReturn403()
    {
        var (client, _) = await GetAuthenticatedClientAsync();

        var r = await client.PutAsJsonAsync("/api/v1/config/source-types/3",
            new { name = "Empresarial" });

        r.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "PUT /config/fortnight-types por usuário comum deve retornar 403")]
    public async Task UpdateFortnightType_NonAdmin_ShouldReturn403()
    {
        var (client, _) = await GetAuthenticatedClientAsync();

        var r = await client.PutAsJsonAsync("/api/v1/config/fortnight-types/3",
            new { name = "Third" });

        r.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── PUT seed — admin deve receber 400 ────────────────────────────────────

    [Fact(DisplayName = "PUT /config/payment-statuses/1 (seed) por admin deve retornar 400")]
    public async Task UpdatePaymentStatus_SeedById_ShouldReturn400()
    {
        var (client, _) = await GetAdminAuthenticatedClientAsync();

        var r = await client.PutAsJsonAsync("/api/v1/config/payment-statuses/1",
            new { name = "Alterado", description = "Desc" });

        r.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── PUT custom — admin deve retornar 200 ─────────────────────────────────

    [Fact(DisplayName = "PUT /config/payment-statuses (item customizado) por admin deve retornar 200")]
    public async Task UpdatePaymentStatus_Custom_ShouldReturn200()
    {
        var (client, _) = await GetAdminAuthenticatedClientAsync();

        // Cria item custom
        var created = await client.PostAsJsonAsync("/api/v1/config/payment-statuses",
            new { name = $"Custom_{Guid.NewGuid():N}", description = "Desc" });
        var createdBody = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = createdBody.GetProperty("id").GetInt32();

        var r = await client.PutAsJsonAsync($"/api/v1/config/payment-statuses/{id}",
            new { name = $"Atualizado_{Guid.NewGuid():N}", description = "Nova Desc" });

        r.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── DELETE — não-admin deve receber 403 ──────────────────────────────────

    [Fact(DisplayName = "DELETE /config/payment-statuses por usuário comum deve retornar 403")]
    public async Task DeletePaymentStatus_NonAdmin_ShouldReturn403()
    {
        var (client, _) = await GetAuthenticatedClientAsync();

        var r = await client.DeleteAsync("/api/v1/config/payment-statuses/5");

        r.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "DELETE /config/source-types por usuário comum deve retornar 403")]
    public async Task DeleteSourceType_NonAdmin_ShouldReturn403()
    {
        var (client, _) = await GetAuthenticatedClientAsync();

        var r = await client.DeleteAsync("/api/v1/config/source-types/3");

        r.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "DELETE /config/fortnight-types por usuário comum deve retornar 403")]
    public async Task DeleteFortnightType_NonAdmin_ShouldReturn403()
    {
        var (client, _) = await GetAuthenticatedClientAsync();

        var r = await client.DeleteAsync("/api/v1/config/fortnight-types/3");

        r.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── DELETE seed — admin deve receber 400 ─────────────────────────────────

    [Fact(DisplayName = "DELETE /config/payment-statuses/1 (seed) por admin deve retornar 400")]
    public async Task DeletePaymentStatus_Seed_ShouldReturn400()
    {
        var (client, _) = await GetAdminAuthenticatedClientAsync();

        var r = await client.DeleteAsync("/api/v1/config/payment-statuses/1");

        r.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── DELETE custom — admin deve retornar 204 ───────────────────────────────

    [Fact(DisplayName = "DELETE /config/payment-statuses (item customizado) por admin deve retornar 204")]
    public async Task DeletePaymentStatus_Custom_ShouldReturn204()
    {
        var (client, _) = await GetAdminAuthenticatedClientAsync();

        var created = await client.PostAsJsonAsync("/api/v1/config/payment-statuses",
            new { name = $"Custom_{Guid.NewGuid():N}", description = "Desc" });
        var createdBody = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = createdBody.GetProperty("id").GetInt32();

        var r = await client.DeleteAsync($"/api/v1/config/payment-statuses/{id}");

        r.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // ── Source Type — admin happy path ────────────────────────────────────────

    [Fact(DisplayName = "POST /config/source-types por admin deve retornar 201")]
    public async Task CreateSourceType_Admin_ShouldReturn201()
    {
        var (client, _) = await GetAdminAuthenticatedClientAsync();

        var r = await client.PostAsJsonAsync("/api/v1/config/source-types",
            new { name = $"Empresarial_{Guid.NewGuid():N}" });

        r.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact(DisplayName = "PUT /config/source-types/{id} (seed) por admin deve retornar 400")]
    public async Task UpdateSourceType_Seed_ShouldReturn400()
    {
        var (client, _) = await GetAdminAuthenticatedClientAsync();

        var r = await client.PutAsJsonAsync("/api/v1/config/source-types/1",
            new { name = "Alterado" });

        r.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "PUT /config/source-types/{id} (item customizado) por admin deve retornar 200")]
    public async Task UpdateSourceType_Custom_ShouldReturn200()
    {
        var (client, _) = await GetAdminAuthenticatedClientAsync();

        var created = await client.PostAsJsonAsync("/api/v1/config/source-types",
            new { name = $"Src_{Guid.NewGuid():N}" });
        var createdBody = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = createdBody.GetProperty("id").GetInt32();

        var r = await client.PutAsJsonAsync($"/api/v1/config/source-types/{id}",
            new { name = $"SrcAtualizado_{Guid.NewGuid():N}" });

        r.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "DELETE /config/source-types/{id} (seed) por admin deve retornar 400")]
    public async Task DeleteSourceType_Seed_ShouldReturn400()
    {
        var (client, _) = await GetAdminAuthenticatedClientAsync();

        var r = await client.DeleteAsync("/api/v1/config/source-types/1");

        r.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "DELETE /config/source-types/{id} (item customizado) por admin deve retornar 204")]
    public async Task DeleteSourceType_Custom_ShouldReturn204()
    {
        var (client, _) = await GetAdminAuthenticatedClientAsync();

        var created = await client.PostAsJsonAsync("/api/v1/config/source-types",
            new { name = $"SrcDel_{Guid.NewGuid():N}" });
        var createdBody = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = createdBody.GetProperty("id").GetInt32();

        var r = await client.DeleteAsync($"/api/v1/config/source-types/{id}");

        r.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // ── Fortnight Type — admin happy path ────────────────────────────────────

    [Fact(DisplayName = "POST /config/fortnight-types por admin deve retornar 201")]
    public async Task CreateFortnightType_Admin_ShouldReturn201()
    {
        var (client, _) = await GetAdminAuthenticatedClientAsync();

        var r = await client.PostAsJsonAsync("/api/v1/config/fortnight-types",
            new { name = $"Quinzena_{Guid.NewGuid():N}" });

        r.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact(DisplayName = "PUT /config/fortnight-types/{id} (seed) por admin deve retornar 400")]
    public async Task UpdateFortnightType_Seed_ShouldReturn400()
    {
        var (client, _) = await GetAdminAuthenticatedClientAsync();

        var r = await client.PutAsJsonAsync("/api/v1/config/fortnight-types/1",
            new { name = "Alterado" });

        r.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "PUT /config/fortnight-types/{id} (item customizado) por admin deve retornar 200")]
    public async Task UpdateFortnightType_Custom_ShouldReturn200()
    {
        var (client, _) = await GetAdminAuthenticatedClientAsync();

        var created = await client.PostAsJsonAsync("/api/v1/config/fortnight-types",
            new { name = $"Fort_{Guid.NewGuid():N}" });
        var createdBody = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = createdBody.GetProperty("id").GetInt32();

        var r = await client.PutAsJsonAsync($"/api/v1/config/fortnight-types/{id}",
            new { name = $"FortAtualizado_{Guid.NewGuid():N}" });

        r.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "DELETE /config/fortnight-types/{id} (seed) por admin deve retornar 400")]
    public async Task DeleteFortnightType_Seed_ShouldReturn400()
    {
        var (client, _) = await GetAdminAuthenticatedClientAsync();

        var r = await client.DeleteAsync("/api/v1/config/fortnight-types/1");

        r.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "DELETE /config/fortnight-types/{id} (item customizado) por admin deve retornar 204")]
    public async Task DeleteFortnightType_Custom_ShouldReturn204()
    {
        var (client, _) = await GetAdminAuthenticatedClientAsync();

        var created = await client.PostAsJsonAsync("/api/v1/config/fortnight-types",
            new { name = $"FortDel_{Guid.NewGuid():N}" });
        var createdBody = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = createdBody.GetProperty("id").GetInt32();

        var r = await client.DeleteAsync($"/api/v1/config/fortnight-types/{id}");

        r.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
