using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace PersonalFinance.Api.Tests.Integration
{
    public class PeriodsControllerTests : ApiIntegrationTestBase
    {
        public PeriodsControllerTests(TestWebApplicationFactory f) : base(f) { }

        [Fact(DisplayName = "GET /periods deve retornar 200 com lista")]
        public async Task GetAll_ShouldReturn200()
        {
            var (client, _) = await GetAuthenticatedClientAsync();
            var r = await client.GetAsync("/api/v1/periods");
            r.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact(DisplayName = "POST e GET /periods deve persistir e retornar o período")]
        public async Task CreateAndGet_ShouldPersistAndReturn()
        {
            var (client, _) = await GetAuthenticatedClientAsync();

            var created = await client.PostAsJsonAsync("/api/v1/periods", new { year = 2026, month = 4 });
            created.StatusCode.Should().Be(HttpStatusCode.Created);
            var body = await created.Content.ReadFromJsonAsync<JsonElement>();
            var id = body.GetProperty("id").GetString()!;

            var getById = await client.GetAsync($"/api/v1/periods/{id}");
            getById.StatusCode.Should().Be(HttpStatusCode.OK);
            var period = await getById.Content.ReadFromJsonAsync<JsonElement>();
            period.GetProperty("month").GetInt32().Should().Be(4);
            period.GetProperty("year").GetInt32().Should().Be(2026);
        }

        [Fact(DisplayName = "POST /periods deve retornar 400 para mês inválido")]
        public async Task Create_WithInvalidMonth_ShouldReturn400()
        {
            var (client, _) = await GetAuthenticatedClientAsync();
            var r = await client.PostAsJsonAsync("/api/v1/periods", new { year = 2026, month = 13 });
            r.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact(DisplayName = "POST /periods deve retornar 400 para período duplicado")]
        public async Task Create_Duplicate_ShouldReturn400()
        {
            var (client, _) = await GetAuthenticatedClientAsync();
            await client.PostAsJsonAsync("/api/v1/periods", new { year = 2026, month = 5 });
            var r = await client.PostAsJsonAsync("/api/v1/periods", new { year = 2026, month = 5 });
            r.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact(DisplayName = "GET /periods/{id}/summary deve retornar 200 com balance zero para período vazio")]
        public async Task GetSummary_EmptyPeriod_ShouldReturnZeroBalance()
        {
            var (client, _) = await GetAuthenticatedClientAsync();
            var created = await client.PostAsJsonAsync("/api/v1/periods", new { year = 2026, month = 6 });
            var body = await created.Content.ReadFromJsonAsync<JsonElement>();
            var id = body.GetProperty("id").GetString();

            var r = await client.GetAsync($"/api/v1/periods/{id}/summary");
            r.StatusCode.Should().Be(HttpStatusCode.OK);
            var summary = await r.Content.ReadFromJsonAsync<JsonElement>();
            summary.GetProperty("balance").GetDecimal().Should().Be(0m);
        }

        [Fact(DisplayName = "DELETE /periods/{id} vazio deve retornar 204")]
        public async Task Delete_EmptyPeriod_ShouldReturn204()
        {
            var (client, _) = await GetAuthenticatedClientAsync();
            var created = await client.PostAsJsonAsync("/api/v1/periods", new { year = 2026, month = 9 });
            var body = await created.Content.ReadFromJsonAsync<JsonElement>();
            var id = body.GetProperty("id").GetString();

            var r = await client.DeleteAsync($"/api/v1/periods/{id}");

            r.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        [Fact(DisplayName = "PATCH /periods/{id}/toggle-active deve retornar 204")]
        public async Task ToggleActive_ShouldReturn204()
        {
            var (client, _) = await GetAuthenticatedClientAsync();
            var created = await client.PostAsJsonAsync("/api/v1/periods", new { year = 2026, month = 10 });
            var body = await created.Content.ReadFromJsonAsync<JsonElement>();
            var id = body.GetProperty("id").GetString();

            var r = await client.PatchAsync($"/api/v1/periods/{id}/toggle-active", null);

            r.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        [Fact(DisplayName = "GET /periods/{id} de outro usuário deve retornar 404")]
        public async Task GetById_FromOtherUser_ShouldReturn404()
        {
            var (client1, _) = await GetAuthenticatedClientAsync();
            var (client2, _) = await GetAuthenticatedClientAsync();

            var created = await client1.PostAsJsonAsync("/api/v1/periods", new { year = 2026, month = 7 });
            var body = await created.Content.ReadFromJsonAsync<JsonElement>();
            var id = body.GetProperty("id").GetString();

            var r = await client2.GetAsync($"/api/v1/periods/{id}");
            r.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
    }
}
