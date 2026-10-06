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

            // O admin já é semeado pela factory (com role Admin e e-mail confirmado). O register agora responde
            // 202 genérico sem id (#404), então o id vem do claim sub do JWT do login.
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

        /// <summary>
        /// Registra um usuário (202 genérico, sem id — #404) e devolve o Id obtido do claim sub do JWT do login.
        /// </summary>
        protected async Task<Guid> RegisterAndGetUserIdAsync(string name, string email, string password)
        {
            var register = await Client.PostAsJsonAsync("/api/v1/auth/register", new { name, email, password });
            register.EnsureSuccessStatusCode();

            var loginResponse = await Client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
            var body = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
            var token = body.GetProperty("token").GetString()!;

            var parts = token.Split('.');
            var padded = parts[1].PadRight(parts[1].Length + (4 - parts[1].Length % 4) % 4, '=');
            var payload = JsonSerializer.Deserialize<JsonElement>(
                System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(padded)));
            return Guid.Parse(payload.GetProperty("sub").GetString()!);
        }

        public void Dispose() => Client.Dispose();
    }
}
