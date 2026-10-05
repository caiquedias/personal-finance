using FluentAssertions;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using PersonalFinance.Application.DTOs.Admin;
using PersonalFinance.Application.DTOs.Auth;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace PersonalFinance.Api.Tests.Integration;

/// <summary>Validação FluentValidation de Auth e Admin (#396): DI, mapeamento 400 e regressão de contratos.</summary>
public class AuthAdminValidationTests : ApiIntegrationTestBase
{
    private readonly TestWebApplicationFactory _factory;

    public AuthAdminValidationTests(TestWebApplicationFactory factory) : base(factory) => _factory = factory;

    private static string Str(int n) => new('a', n);

    private async Task<string> RegisterTargetAsync()
    {
        var reg = await Client.PostAsJsonAsync("/api/v1/auth/register",
            new { name = "Alvo", email = $"alvo_{Guid.NewGuid():N}@test.com", password = "Senha@Teste123" });
        var body = await reg.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("id").GetString()!;
    }

    private static async Task AssertValidationPayloadAsync(HttpResponseMessage r)
    {
        r.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await r.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("message").GetString().Should().NotBeNullOrWhiteSpace();
        body.TryGetProperty("status", out _).Should().BeTrue();
        body.TryGetProperty("traceId", out _).Should().BeTrue();
        body.TryGetProperty("errors", out _).Should().BeFalse("o contrato é {status,error,message,traceId}, sem ProblemDetails");
    }

    // ── DI ────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "IValidator<T> deve ser resolvível para os 6 DTOs de Auth e Admin")]
    public void Validators_ShouldBeResolvable()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;

        sp.GetService<IValidator<RegisterUserDto>>().Should().NotBeNull();
        sp.GetService<IValidator<LoginDto>>().Should().NotBeNull();
        sp.GetService<IValidator<CreateUserByAdminDto>>().Should().NotBeNull();
        sp.GetService<IValidator<UpdateUserByAdminDto>>().Should().NotBeNull();
        sp.GetService<IValidator<AssignRoleDto>>().Should().NotBeNull();
        sp.GetService<IValidator<ResetPasswordDto>>().Should().NotBeNull();
    }

    // ── Auth ──────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "POST /register com senha de 7 caracteres deve retornar 400 com message")]
    public async Task Register_WithShortPassword_ShouldReturn400WithMessage()
    {
        var r = await Client.PostAsJsonAsync("/api/v1/auth/register",
            new { name = "X", email = $"s_{Guid.NewGuid():N}@x.com", password = "Abc@123" });
        await AssertValidationPayloadAsync(r);
    }

    [Fact(DisplayName = "POST /register com senha acima de 128 caracteres deve retornar 400")]
    public async Task Register_WithHugePassword_ShouldReturn400()
    {
        var r = await Client.PostAsJsonAsync("/api/v1/auth/register",
            new { name = "X", email = $"h_{Guid.NewGuid():N}@x.com", password = Str(129) });
        await AssertValidationPayloadAsync(r);
    }

    [Fact(DisplayName = "POST /register com nome vazio deve retornar 400 com message")]
    public async Task Register_WithEmptyName_ShouldReturn400WithMessage()
    {
        var r = await Client.PostAsJsonAsync("/api/v1/auth/register",
            new { name = "", email = $"n_{Guid.NewGuid():N}@x.com", password = "Senha@123" });
        await AssertValidationPayloadAsync(r);
    }

    [Fact(DisplayName = "POST /login com e-mail ausente deve retornar 400 (não 500)")]
    public async Task Login_WithMissingEmail_ShouldReturn400()
    {
        var r = await Client.PostAsJsonAsync("/api/v1/auth/login", new { password = "Senha@123" });
        await AssertValidationPayloadAsync(r);
    }

    [Fact(DisplayName = "POST /login com e-mail vazio deve retornar 400 com message")]
    public async Task Login_WithEmptyEmail_ShouldReturn400()
    {
        var r = await Client.PostAsJsonAsync("/api/v1/auth/login", new { email = "", password = "Senha@123" });
        await AssertValidationPayloadAsync(r);
    }

    [Fact(DisplayName = "POST /login com senha vazia deve retornar 400 de validação")]
    public async Task Login_WithEmptyPassword_ShouldReturnValidation400()
    {
        var r = await Client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = "alguem@x.com", password = "" });
        await AssertValidationPayloadAsync(r);
        (await r.Content.ReadAsStringAsync()).Should().NotContain("Credenciais inválidas.");
    }

    [Fact(DisplayName = "POST /login com senha acima de 128 caracteres deve retornar 400 de validação")]
    public async Task Login_WithHugePassword_ShouldReturnValidation400()
    {
        var r = await Client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = "alguem@x.com", password = Str(129) });
        await AssertValidationPayloadAsync(r);
        (await r.Content.ReadAsStringAsync()).Should().NotContain("Credenciais inválidas.");
    }

    [Fact(DisplayName = "POST /login com senha curta incorreta mantém Credenciais inválidas e conta para lockout")]
    public async Task Login_WithShortWrongPassword_ShouldKeepCredentialsErrorAndLockout()
    {
        var email = $"lk_{Guid.NewGuid():N}@x.com";
        await Client.PostAsJsonAsync("/api/v1/auth/register",
            new { name = "Lk", email, password = "Senha@123" });

        var first = await Client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "Errada" });
        first.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await first.Content.ReadAsStringAsync()).Should().Contain("Credenciais inválidas.");

        for (var i = 0; i < 4; i++)
            await Client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "Errada" });

        var correct = await Client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "Senha@123" });
        correct.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await correct.Content.ReadAsStringAsync()).Should().Contain("Credenciais inválidas.");
    }

    // ── Admin — criação ───────────────────────────────────────────────────────

    [Fact(DisplayName = "POST /admin/users com senha acima de 128 caracteres deve retornar 400 com message")]
    public async Task AdminCreate_WithHugePassword_ShouldReturn400()
    {
        var (admin, _) = await GetAdminAuthenticatedClientAsync();
        var r = await admin.PostAsJsonAsync("/api/v1/admin/users",
            new { name = "Novo", email = $"c_{Guid.NewGuid():N}@x.com", password = Str(129) });
        await AssertValidationPayloadAsync(r);
    }

    [Fact(DisplayName = "POST /admin/users com e-mail acima de 200 caracteres deve retornar 400 com message")]
    public async Task AdminCreate_WithHugeEmail_ShouldReturn400()
    {
        var (admin, _) = await GetAdminAuthenticatedClientAsync();
        var r = await admin.PostAsJsonAsync("/api/v1/admin/users",
            new { name = "Novo", email = Str(201) + "@x.com", password = "Senha@123" });
        await AssertValidationPayloadAsync(r);
    }

    [Fact(DisplayName = "POST /admin/users com dados válidos deve retornar 201")]
    public async Task AdminCreate_WithValidData_ShouldReturn201()
    {
        var (admin, _) = await GetAdminAuthenticatedClientAsync();
        var r = await admin.PostAsJsonAsync("/api/v1/admin/users",
            new { name = "Novo", email = $"ok_{Guid.NewGuid():N}@x.com", password = "Senha@123" });
        r.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    // ── Admin — body sem UserId (vem da rota) continua válido ────────────────

    [Fact(DisplayName = "PUT /admin/users/{id} com body sem UserId deve retornar 200")]
    public async Task AdminUpdate_BodyWithoutUserId_ShouldReturn200()
    {
        var (admin, _) = await GetAdminAuthenticatedClientAsync();
        var id = await RegisterTargetAsync();

        var r = await admin.PutAsJsonAsync($"/api/v1/admin/users/{id}", new { name = "Renomeado" });

        r.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "PUT /admin/users/{id} com nome acima de 100 caracteres deve retornar 400 com message")]
    public async Task AdminUpdate_WithHugeName_ShouldReturn400()
    {
        var (admin, _) = await GetAdminAuthenticatedClientAsync();
        var id = await RegisterTargetAsync();

        var r = await admin.PutAsJsonAsync($"/api/v1/admin/users/{id}", new { name = Str(101) });

        await AssertValidationPayloadAsync(r);
    }

    [Fact(DisplayName = "POST /admin/users/{id}/roles com body sem UserId deve retornar 204")]
    public async Task AdminAssignRole_BodyWithoutUserId_ShouldReturn204()
    {
        var (admin, _) = await GetAdminAuthenticatedClientAsync();
        var id = await RegisterTargetAsync();

        var r = await admin.PostAsJsonAsync($"/api/v1/admin/users/{id}/roles", new { roleId = 2 });

        r.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Theory(DisplayName = "POST /admin/users/{id}/roles com roleId não positivo deve retornar 400 com message")]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task AdminAssignRole_WithNonPositiveRoleId_ShouldReturn400(int roleId)
    {
        var (admin, _) = await GetAdminAuthenticatedClientAsync();
        var id = await RegisterTargetAsync();

        var r = await admin.PostAsJsonAsync($"/api/v1/admin/users/{id}/roles", new { roleId });

        await AssertValidationPayloadAsync(r);
    }

    [Fact(DisplayName = "PATCH /admin/users/{id}/reset-password com body sem UserId deve retornar 204")]
    public async Task AdminResetPassword_BodyWithoutUserId_ShouldReturn204()
    {
        var (admin, _) = await GetAdminAuthenticatedClientAsync();
        var id = await RegisterTargetAsync();

        var r = await admin.PatchAsJsonAsync($"/api/v1/admin/users/{id}/reset-password",
            new { newPassword = "NovaSenha@456" });

        r.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact(DisplayName = "PATCH /admin/users/{id}/reset-password com senha acima de 128 caracteres deve retornar 400 com message")]
    public async Task AdminResetPassword_WithHugePassword_ShouldReturn400()
    {
        var (admin, _) = await GetAdminAuthenticatedClientAsync();
        var id = await RegisterTargetAsync();

        var r = await admin.PatchAsJsonAsync($"/api/v1/admin/users/{id}/reset-password",
            new { newPassword = Str(129) });

        await AssertValidationPayloadAsync(r);
    }
}
