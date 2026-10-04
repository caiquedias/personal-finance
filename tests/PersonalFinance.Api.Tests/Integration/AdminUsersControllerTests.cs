using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Domain.Entities.Auth;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;
using static PersonalFinance.Api.Tests.Integration.MfaTestHelper;

namespace PersonalFinance.Api.Tests.Integration;

public class AdminUsersControllerTests : ApiIntegrationTestBase
{
    private readonly TestWebApplicationFactory _mfaFactory;

    public AdminUsersControllerTests(TestWebApplicationFactory factory) : base(factory) => _mfaFactory = factory;

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

    // ── Reset de MFA (#479) ───────────────────────────────────────────────────

    [Fact(DisplayName = "POST /admin/users/{id}/mfa/reset sem token deve retornar 401")]
    public async Task ResetMfa_WithoutToken_ShouldReturn401()
    {
        var r = await Client.PostAsync($"/api/v1/admin/users/{Guid.NewGuid()}/mfa/reset", null);
        r.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "POST /admin/users/{id}/mfa/reset por não-admin deve retornar 403")]
    public async Task ResetMfa_NonAdmin_ShouldReturn403()
    {
        var (client, _) = await GetAuthenticatedClientAsync();

        var r = await client.PostAsync($"/api/v1/admin/users/{Guid.NewGuid()}/mfa/reset", null);

        r.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "POST /admin/users/{id}/mfa/reset por admin deve retornar 204 e limpar MFA e recovery codes")]
    public async Task ResetMfa_Admin_ShouldReturn204AndClearMfa()
    {
        var (adminClient, _) = await GetAdminAuthenticatedClientAsync();
        var target = await CreateMfaUserAsync(_mfaFactory);

        var r = await adminClient.PostAsync($"/api/v1/admin/users/{target.UserId}/mfa/reset", null);

        r.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var user = await GetUserAsync(_mfaFactory, target.UserId);
        user.MfaEnabled.Should().BeFalse();
        user.MfaSecretEncrypted.Should().BeNull();
        var activeCodes = await WithDbAsync(_mfaFactory, db =>
            db.Set<MfaRecoveryCode>().CountAsync(c => c.UserId == target.UserId));
        activeCodes.Should().Be(0);
        var softDeleted = await WithDbAsync(_mfaFactory, db =>
            db.Set<MfaRecoveryCode>().IgnoreQueryFilters()
              .CountAsync(c => c.UserId == target.UserId && c.DeletedAt != null));
        softDeleted.Should().BeGreaterThan(0);
    }

    // ── Invalidação de sessões do alvo (#489) ─────────────────────────────────

    private const string ProtectedPath = "/api/v1/periods";

    /// <summary>Cadastra e loga o usuário-alvo guardando o token completo emitido antes da ação admin.</summary>
    private async Task<(Guid UserId, string Email, string OldToken)> LoginTargetAsync()
    {
        var anonymous = _mfaFactory.CreateClient();
        var email = await RegisterUserAsync(anonymous);
        var login = await ReadJsonAsync(await anonymous.PostAsJsonAsync(LoginPath, new { email, password = Password }));
        var userId = await WithDbAsync(_mfaFactory, db =>
            db.Users.Where(u => u.Email == email).Select(u => u.Id).SingleAsync());
        return (userId, email, login.GetProperty("token").GetString()!);
    }

    [Fact(DisplayName = "Reset de MFA pelo admin deve invalidar o token anterior do alvo (401) e permitir novo login")]
    public async Task ResetMfa_Admin_ShouldInvalidateTargetOldToken()
    {
        var (adminClient, _) = await GetAdminAuthenticatedClientAsync();
        var (userId, email, oldToken) = await LoginTargetAsync();
        var target = WithBearer(_mfaFactory, oldToken);
        (await target.PostAsync(SetupPath, null)).EnsureSuccessStatusCode();
        (await target.GetAsync(ProtectedPath)).StatusCode.Should().Be(HttpStatusCode.OK);

        var r = await adminClient.PostAsync($"/api/v1/admin/users/{userId}/mfa/reset", null);

        r.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await WithBearer(_mfaFactory, oldToken).GetAsync(ProtectedPath))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var login = await ReadJsonAsync(await _mfaFactory.CreateClient()
            .PostAsJsonAsync(LoginPath, new { email, password = Password }));
        (await WithBearer(_mfaFactory, login.GetProperty("token").GetString()!).GetAsync(ProtectedPath))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "Reset de senha pelo admin deve invalidar o token anterior do alvo (401)")]
    public async Task ResetPassword_Admin_ShouldInvalidateTargetOldToken()
    {
        var (adminClient, _) = await GetAdminAuthenticatedClientAsync();
        var (userId, _, oldToken) = await LoginTargetAsync();
        (await WithBearer(_mfaFactory, oldToken).GetAsync(ProtectedPath)).StatusCode.Should().Be(HttpStatusCode.OK);

        var r = await adminClient.PatchAsJsonAsync(
            $"/api/v1/admin/users/{userId}/reset-password",
            new { newPassword = "NovaSenha@456" });

        r.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await WithBearer(_mfaFactory, oldToken).GetAsync(ProtectedPath))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "Desativar e reativar o usuário não deve revalidar o token anterior (401)")]
    public async Task ToggleActive_DeactivateThenReactivate_ShouldKeepOldTokenInvalid()
    {
        var (adminClient, _) = await GetAdminAuthenticatedClientAsync();
        var (userId, _, oldToken) = await LoginTargetAsync();

        (await adminClient.PatchAsync($"/api/v1/admin/users/{userId}/toggle-active", null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await adminClient.PatchAsync($"/api/v1/admin/users/{userId}/toggle-active", null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await WithBearer(_mfaFactory, oldToken).GetAsync(ProtectedPath))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "POST /admin/users/{id}/mfa/reset com id inexistente deve retornar 404")]
    public async Task ResetMfa_UnknownUser_ShouldReturn404()
    {
        var (adminClient, _) = await GetAdminAuthenticatedClientAsync();

        var r = await adminClient.PostAsync($"/api/v1/admin/users/{Guid.NewGuid()}/mfa/reset", null);

        r.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "POST /admin/users/{id}/mfa/reset no próprio admin deve retornar 400")]
    public async Task ResetMfa_OwnAdmin_ShouldReturn400()
    {
        var (adminClient, adminId) = await GetAdminAuthenticatedClientAsync();

        var r = await adminClient.PostAsync($"/api/v1/admin/users/{adminId}/mfa/reset", null);

        r.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
