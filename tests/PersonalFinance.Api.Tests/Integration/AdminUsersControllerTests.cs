using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace PersonalFinance.Api.Tests.Integration;

public class AdminUsersControllerTests : ApiIntegrationTestBase
{
    public AdminUsersControllerTests(TestWebApplicationFactory factory) : base(factory) { }

    // ── Sem autenticação ──────────────────────────────────────────────────────

    [Fact(DisplayName = "GET /admin/users sem token deve retornar 401")]
    public async Task GetAll_WithoutToken_ShouldReturn401()
    {
        var r = await Client.GetAsync("/api/v1/admin/users");
        r.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── Usuário comum não tem acesso ──────────────────────────────────────────

    [Fact(DisplayName = "GET /admin/users por usuário sem role Admin deve retornar 403")]
    public async Task GetAll_NonAdmin_ShouldReturn403()
    {
        var (client, _) = await GetAuthenticatedClientAsync();

        var r = await client.GetAsync("/api/v1/admin/users");

        r.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "PATCH /admin/users/{id}/toggle-active por não-admin deve retornar 403")]
    public async Task ToggleActive_NonAdmin_ShouldReturn403()
    {
        var (client, _) = await GetAuthenticatedClientAsync();

        var r = await client.PatchAsync(
            $"/api/v1/admin/users/{Guid.NewGuid()}/toggle-active", null);

        r.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "POST /admin/users/{id}/roles por não-admin deve retornar 403")]
    public async Task AssignRole_NonAdmin_ShouldReturn403()
    {
        var (client, _) = await GetAuthenticatedClientAsync();

        var r = await client.PostAsJsonAsync(
            $"/api/v1/admin/users/{Guid.NewGuid()}/roles",
            new { roleId = 1 });

        r.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "DELETE /admin/users/{id}/roles/{roleId} por não-admin deve retornar 403")]
    public async Task RemoveRole_NonAdmin_ShouldReturn403()
    {
        var (client, _) = await GetAuthenticatedClientAsync();

        var r = await client.DeleteAsync(
            $"/api/v1/admin/users/{Guid.NewGuid()}/roles/2");

        r.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "PATCH /admin/users/{id}/reset-password por não-admin deve retornar 403")]
    public async Task ResetPassword_NonAdmin_ShouldReturn403()
    {
        var (client, _) = await GetAuthenticatedClientAsync();

        var r = await client.PatchAsJsonAsync(
            $"/api/v1/admin/users/{Guid.NewGuid()}/reset-password",
            new { newPassword = "NovaSenha@123" });

        r.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── Admin — happy paths ───────────────────────────────────────────────────

    [Fact(DisplayName = "GET /admin/users por admin deve retornar 200 com ao menos 1 usuário")]
    public async Task GetAll_Admin_ShouldReturnUsers()
    {
        var (client, _) = await GetAdminAuthenticatedClientAsync();

        var r = await client.GetAsync("/api/v1/admin/users");
        var body = await r.Content.ReadFromJsonAsync<JsonElement>();

        r.StatusCode.Should().Be(HttpStatusCode.OK);
        body.GetProperty("items").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact(DisplayName = "PATCH /admin/users/{id}/toggle-active por admin deve retornar 204")]
    public async Task ToggleActive_Admin_ShouldReturn204()
    {
        var (adminClient, _) = await GetAdminAuthenticatedClientAsync();

        // Cria usuário-alvo
        var email = $"target_{Guid.NewGuid():N}@test.com";
        var reg = await Client.PostAsJsonAsync("/api/v1/auth/register",
            new { name = "Target", email, password = "Senha@Teste123" });
        var regBody = await reg.Content.ReadFromJsonAsync<JsonElement>();
        var targetId = regBody.GetProperty("id").GetString()!;

        var r = await adminClient.PatchAsync(
            $"/api/v1/admin/users/{targetId}/toggle-active", null);

        r.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact(DisplayName = "POST /admin/users/{id}/roles por admin deve retornar 204")]
    public async Task AssignRole_Admin_ShouldReturn204()
    {
        var (adminClient, _) = await GetAdminAuthenticatedClientAsync();

        var email = $"assign_{Guid.NewGuid():N}@test.com";
        var reg = await Client.PostAsJsonAsync("/api/v1/auth/register",
            new { name = "Assign", email, password = "Senha@Teste123" });
        var regBody = await reg.Content.ReadFromJsonAsync<JsonElement>();
        var targetId = regBody.GetProperty("id").GetString()!;

        var r = await adminClient.PostAsJsonAsync(
            $"/api/v1/admin/users/{targetId}/roles",
            new { roleId = 2 });

        r.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact(DisplayName = "DELETE /admin/users/{id}/roles/{roleId} por admin deve retornar 204")]
    public async Task RemoveRole_Admin_ShouldReturn204()
    {
        var (adminClient, _) = await GetAdminAuthenticatedClientAsync();

        var email = $"remove_{Guid.NewGuid():N}@test.com";
        var reg = await Client.PostAsJsonAsync("/api/v1/auth/register",
            new { name = "Remove", email, password = "Senha@Teste123" });
        var regBody = await reg.Content.ReadFromJsonAsync<JsonElement>();
        var targetId = regBody.GetProperty("id").GetString()!;

        // Atribui role 2 ao usuário
        await adminClient.PostAsJsonAsync(
            $"/api/v1/admin/users/{targetId}/roles",
            new { roleId = 2 });

        // Remove role 2
        var r = await adminClient.DeleteAsync(
            $"/api/v1/admin/users/{targetId}/roles/2");

        r.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact(DisplayName = "PATCH /admin/users/{id}/reset-password por admin deve retornar 204")]
    public async Task ResetPassword_Admin_ShouldReturn204()
    {
        var (adminClient, _) = await GetAdminAuthenticatedClientAsync();

        var email = $"reset_{Guid.NewGuid():N}@test.com";
        var reg = await Client.PostAsJsonAsync("/api/v1/auth/register",
            new { name = "Reset", email, password = "Senha@Teste123" });
        var regBody = await reg.Content.ReadFromJsonAsync<JsonElement>();
        var targetId = regBody.GetProperty("id").GetString()!;

        var r = await adminClient.PatchAsJsonAsync(
            $"/api/v1/admin/users/{targetId}/reset-password",
            new { newPassword = "NovaSenha@456" });

        r.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
