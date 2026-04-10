using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace PersonalFinance.Api.Tests.Integration
{
    /// <summary>Base para todos os testes de integração.</summary>
    public abstract class ApiIntegrationTestBase
    : IClassFixture<TestWebApplicationFactory>, IDisposable
    {
        protected readonly HttpClient Client;
        private readonly TestWebApplicationFactory _factory;

        protected ApiIntegrationTestBase(TestWebApplicationFactory factory)
        {
            _factory = factory;
            Client = factory.CreateClient();
        }

        protected async Task<(HttpClient client, Guid userId)> GetAuthenticatedClientAsync()
        {
            var email = $"test_{Guid.NewGuid():N}@monkeybomb.com";
            var password = "Senha@Teste123";

            await Client.PostAsJsonAsync("/api/v1/auth/register",
                new { name = "Test User", email, password });

            var loginResponse = await Client.PostAsJsonAsync("/api/v1/auth/login",
                new { email, password });

            var body = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
            var token = body.GetProperty("token").GetString()!;

            var authClient = _factory.CreateClient();
            authClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var parts = token.Split('.');
            var padded = parts[1].PadRight(parts[1].Length + (4 - parts[1].Length % 4) % 4, '=');
            var payload = JsonSerializer.Deserialize<JsonElement>(
                System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(padded)));
            var userId = Guid.Parse(payload.GetProperty("sub").GetString()!);

            return (authClient, userId);
        }

        protected async Task<(HttpClient client, Guid userId)> GetAdminAuthenticatedClientAsync()
        {
            var email = $"caique_dias@outlook.com";
            var password = "Arkham@01";

            var createdUser = await Client.PostAsJsonAsync("/api/v1/auth/register",
                new { name = "Test User", email, password });

            // Tenta obter o ID do usuário recém criado; se e-mail já existia, ignora (idempotente)
            if (createdUser.IsSuccessStatusCode)
            {
                var createdUserResponse = await createdUser.Content.ReadFromJsonAsync<JsonElement>();
                var createdUserId = createdUserResponse.GetProperty("id").GetString()!;
                await Client.PostAsJsonAsync($"/api/v1/admin/users/{createdUserId}/roles",
                    new { UserId = createdUserId, RoleId = 1 });
            }

            var loginResponse = await Client.PostAsJsonAsync("/api/v1/auth/login",
                new { email, password });

            var body = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
            var token = body.GetProperty("token").GetString()!;

            var authClient = _factory.CreateClient();
            authClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var parts = token.Split('.');
            var padded = parts[1].PadRight(parts[1].Length + (4 - parts[1].Length % 4) % 4, '=');
            var payload = JsonSerializer.Deserialize<JsonElement>(
                System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(padded)));
            var userId = Guid.Parse(payload.GetProperty("sub").GetString()!);

            return (authClient, userId);
        }

        public void Dispose() => Client.Dispose();
    }
}
