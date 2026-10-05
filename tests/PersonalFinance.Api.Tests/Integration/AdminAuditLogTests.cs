using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using PersonalFinance.Domain.Entities.Auth;
using PersonalFinance.Domain.Enums;
using PersonalFinance.Infrastructure.Persistence.Context;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace PersonalFinance.Api.Tests.Integration;

/// <summary>
/// Audit log das ações administrativas, ponta a ponta (#402): cada ação grava uma linha com ator, alvo,
/// ação e IP, na mesma transação; falhas não auditam; nada sensível em Details.
/// </summary>
public class AdminAuditLogTests : IDisposable
{
    private const string RemoteIp = "203.0.113.7";
    private const string AdminEmail = "caique_dias@outlook.com";

    private sealed class RemoteIpFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((ctx, nxt) =>
            {
                if (ctx.Request.Headers.TryGetValue("X-Test-Remote", out var remote))
                    ctx.Connection.RemoteIpAddress = IPAddress.Parse(remote.ToString());
                return nxt();
            });
            next(app);
        };
    }

    private readonly TestWebApplicationFactory _baseFactory = new();
    private readonly WebApplicationFactory<Program> _factory;

    public AdminAuditLogTests()
    {
        _factory = _baseFactory.WithWebHostBuilder(b =>
            b.ConfigureServices(s => s.AddSingleton<IStartupFilter>(new RemoteIpFilter())));
    }

    private async Task<HttpClient> AdminClientAsync()
    {
        var anonymous = _factory.CreateClient();
        var login = await anonymous.PostAsJsonAsync("/api/v1/auth/login", new { email = AdminEmail, password = "Arkham@01" });
        var body = await login.Content.ReadFromJsonAsync<JsonElement>();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.GetProperty("token").GetString()!);
        client.DefaultRequestHeaders.Add("X-Test-Remote", RemoteIp);
        return client;
    }

    private async Task<T> WithDbAsync<T>(Func<AppDbContext, Task<T>> action)
    {
        using var scope = _factory.Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private Task<Guid> AdminIdAsync() =>
        WithDbAsync(db => db.Users.Where(u => u.Email == AdminEmail).Select(u => u.Id).SingleAsync());

    private Task<List<AuditLog>> LogsForAsync(Guid targetId) =>
        WithDbAsync(db => db.Set<AuditLog>().Where(l => l.TargetUserId == targetId).OrderBy(l => l.CreatedAt).ToListAsync());

    private static async Task<Guid> CreateUserAsync(HttpClient admin, string email)
    {
        var r = await admin.PostAsJsonAsync("/api/v1/admin/users", new { name = "Alvo Audit", email, password = "SenhaSecreta@123" });
        r.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await r.Content.ReadFromJsonAsync<JsonElement>();
        return Guid.Parse(body.GetProperty("id").GetString()!);
    }

    [Fact(DisplayName = "Ações do admin devem gravar linhas de auditoria com ator, alvo, ação e IP corretos")]
    public async Task AdminActions_ShouldWriteAuditRows()
    {
        var admin = await AdminClientAsync();
        var adminId = await AdminIdAsync();
        var email = $"audit_{Guid.NewGuid():N}@test.com";

        var targetId = await CreateUserAsync(admin, email);
        (await admin.PutAsJsonAsync($"/api/v1/admin/users/{targetId}", new { name = "Nome Atualizado" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await admin.PostAsJsonAsync($"/api/v1/admin/users/{targetId}/roles", new { roleId = 1 }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await admin.DeleteAsync($"/api/v1/admin/users/{targetId}/roles/1"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await admin.PatchAsJsonAsync($"/api/v1/admin/users/{targetId}/reset-password", new { newPassword = "NovaSenha@456" }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await admin.PatchAsync($"/api/v1/admin/users/{targetId}/toggle-active", null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await admin.PatchAsync($"/api/v1/admin/users/{targetId}/toggle-active", null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var logs = await LogsForAsync(targetId);

        logs.Select(l => l.Action).Should().BeEquivalentTo(new[]
        {
            AuditAction.UserCreated, AuditAction.UserUpdated, AuditAction.RoleAssigned, AuditAction.RoleRemoved,
            AuditAction.PasswordReset, AuditAction.UserDeactivated, AuditAction.UserActivated
        });
        logs.Should().OnlyContain(l => l.ActorUserId == adminId);
        logs.Should().OnlyContain(l => l.IpAddress == RemoteIp);
        logs.Should().OnlyContain(l => l.CreatedAt > DateTime.UtcNow.AddMinutes(-5) && l.CreatedAt <= DateTime.UtcNow.AddMinutes(1));
        logs.Where(l => l.Details != null).Should().OnlyContain(l =>
            !l.Details!.Contains("SenhaSecreta@123") && !l.Details.Contains("NovaSenha@456")
            && !l.Details.Contains(email) && !l.Details.Contains("Nome Atualizado"));
    }

    [Fact(DisplayName = "Ação que falha não deve gravar auditoria (auto-desativação do admin)")]
    public async Task FailedAction_ShouldNotWriteAuditRow()
    {
        var admin = await AdminClientAsync();
        var adminId = await AdminIdAsync();

        var r = await admin.PatchAsync($"/api/v1/admin/users/{adminId}/toggle-active", null);

        r.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await LogsForAsync(adminId)).Where(l => l.Action == AuditAction.UserDeactivated).Should().BeEmpty();
    }

    [Fact(DisplayName = "Reset de MFA do alvo sem MFA ativo (400) não deve gravar auditoria")]
    public async Task ResetMfa_WithoutMfa_ShouldNotWriteAuditRow()
    {
        var admin = await AdminClientAsync();
        var targetId = await CreateUserAsync(admin, $"nomfa_{Guid.NewGuid():N}@test.com");

        var r = await admin.PostAsync($"/api/v1/admin/users/{targetId}/mfa/reset", null);

        r.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await LogsForAsync(targetId)).Should().NotContain(l => l.Action == AuditAction.MfaReset);
    }

    public void Dispose()
    {
        _factory.Dispose();
        _baseFactory.Dispose();
    }
}
