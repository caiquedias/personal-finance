using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace PersonalFinance.Api.Tests.Integration
{
    public class CategoriesControllerTests : ApiIntegrationTestBase
    {
        public CategoriesControllerTests(TestWebApplicationFactory f) : base(f) { }

        [Fact(DisplayName = "GET /categories sem token deve retornar 401")]
        public async Task GetAll_WithoutToken_ShouldReturn401()
        {
            var r = await Client.GetAsync("/api/v1/categories");
            r.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact(DisplayName = "GET /categories deve retornar 200 com lista vazia inicialmente")]
        public async Task GetAll_AuthenticatedUser_ShouldReturn200()
        {
            var (client, _) = await GetAuthenticatedClientAsync();
            var r = await client.GetAsync("/api/v1/categories");
            r.StatusCode.Should().Be(HttpStatusCode.OK);
            var body = await r.Content.ReadFromJsonAsync<JsonElement>();
            body.GetArrayLength().Should().BeGreaterThanOrEqualTo(0);
        }

        [Fact(DisplayName = "POST /categories deve retornar 201 e GET deve retornar o registro criado")]
        public async Task CreateAndGet_ShouldPersistAndReturn()
        {
            var (client, _) = await GetAuthenticatedClientAsync();

            var created = await client.PostAsJsonAsync("/api/v1/categories", new
            { name = "Moradia", color = "#1E4D2B", icon = "home" });
            created.StatusCode.Should().Be(HttpStatusCode.Created);

            var body = await created.Content.ReadFromJsonAsync<JsonElement>();
            var id = body.GetProperty("id").GetString()!;

            // GET /{id}
            var getById = await client.GetAsync($"/api/v1/categories/{id}");
            getById.StatusCode.Should().Be(HttpStatusCode.OK);
            var cat = await getById.Content.ReadFromJsonAsync<JsonElement>();
            cat.GetProperty("name").GetString().Should().Be("Moradia");

            // GET / — lista deve conter o registro
            var list = await client.GetAsync("/api/v1/categories");
            var arr = await list.Content.ReadFromJsonAsync<JsonElement>();
            arr.GetArrayLength().Should().BeGreaterThan(0);
        }

        [Fact(DisplayName = "GET /categories/{id} de outro usuário deve retornar 404")]
        public async Task GetById_FromOtherUser_ShouldReturn404()
        {
            var (client1, _) = await GetAuthenticatedClientAsync();
            var (client2, _) = await GetAuthenticatedClientAsync();

            var created = await client1.PostAsJsonAsync("/api/v1/categories", new
            { name = "Privada", color = "#FFFFFF" });
            var body = await created.Content.ReadFromJsonAsync<JsonElement>();
            var id = body.GetProperty("id").GetString();

            var r = await client2.GetAsync($"/api/v1/categories/{id}");
            r.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact(DisplayName = "POST /categories deve retornar 400 para cor inválida")]
        public async Task Create_WithInvalidColor_ShouldReturn400()
        {
            var (client, _) = await GetAuthenticatedClientAsync();
            var r = await client.PostAsJsonAsync("/api/v1/categories", new
            { name = "X", color = "SEM_HASH" });
            r.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact(DisplayName = "DELETE /categories/{id} deve retornar 204")]
        public async Task Delete_ShouldReturn204()
        {
            var (client, _) = await GetAuthenticatedClientAsync();
            var created = await client.PostAsJsonAsync("/api/v1/categories", new
            { name = $"Del_{Guid.NewGuid():N}", color = "#FFFFFF" });
            var body = await created.Content.ReadFromJsonAsync<JsonElement>();
            var id = body.GetProperty("id").GetString();

            var r = await client.DeleteAsync($"/api/v1/categories/{id}");
            r.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        [Fact(DisplayName = "PUT /categories/{id} deve retornar 204")]
        public async Task Update_ShouldReturn204()
        {
            var (client, _) = await GetAuthenticatedClientAsync();
            var created = await client.PostAsJsonAsync("/api/v1/categories", new
            { name = "Alimentação", color = "#FFFFFF" });
            var body = await created.Content.ReadFromJsonAsync<JsonElement>();
            var id = body.GetProperty("id").GetString();

            var r = await client.PutAsJsonAsync($"/api/v1/categories/{id}", new
            { name = "Alimentação Atualizada", color = "#000000", icon = "food" });

            r.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }
    }
}
