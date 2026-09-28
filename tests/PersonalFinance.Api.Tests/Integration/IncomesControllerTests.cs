using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace PersonalFinance.Api.Tests.Integration
{
    public class IncomesControllerTests : ApiIntegrationTestBase
    {
        public IncomesControllerTests(TestWebApplicationFactory f) : base(f) { }

        private async Task<(HttpClient client, string periodId)> SetupAsync()
        {
            var (client, _) = await GetAuthenticatedClientAsync();
            var period = await client.PostAsJsonAsync("/api/v1/periods", new { year = 2026, month = 9 });
            var pBody = await period.Content.ReadFromJsonAsync<JsonElement>();
            return (client, pBody.GetProperty("id").GetString()!);
        }

        [Fact(DisplayName = "GET /incomes?periodId deve retornar 200 com lista vazia")]
        public async Task GetByPeriod_EmptyPeriod_ShouldReturn200WithEmptyList()
        {
            var (client, pid) = await SetupAsync();
            var r = await client.GetAsync($"/api/v1/incomes?periodId={pid}");
            r.StatusCode.Should().Be(HttpStatusCode.OK);
            var paged = await r.Content.ReadFromJsonAsync<JsonElement>();
            paged.GetProperty("items").GetArrayLength().Should().Be(0);
        }

        [Fact(DisplayName = "POST e GET /incomes deve persistir e retornar a receita")]
        public async Task CreateAndGet_ShouldPersistAndReturn()
        {
            var (client, pid) = await SetupAsync();

            var created = await client.PostAsJsonAsync("/api/v1/incomes", new
            {
                periodId = pid,
                fortnightType = 1,
                description = "Adiantamento MDS",
                amount = 5500.00,
                receivedAt = "2026-09-15"
            });
            created.StatusCode.Should().Be(HttpStatusCode.Created);
            var body = await created.Content.ReadFromJsonAsync<JsonElement>();
            var id = body.GetProperty("id").GetString()!;

            // GET /{id}
            var getById = await client.GetAsync($"/api/v1/incomes/{id}");
            getById.StatusCode.Should().Be(HttpStatusCode.OK);
            var inc = await getById.Content.ReadFromJsonAsync<JsonElement>();
            inc.GetProperty("description").GetString().Should().Be("Adiantamento MDS");
            inc.GetProperty("amount").GetDecimal().Should().Be(5500m);

            // GET lista
            var list = await client.GetAsync($"/api/v1/incomes?periodId={pid}");
            var paged = await list.Content.ReadFromJsonAsync<JsonElement>();
            paged.GetProperty("items").GetArrayLength().Should().Be(1);
        }

        [Fact(DisplayName = "POST /incomes deve retornar 400 para amount negativo")]
        public async Task Create_WithNegativeAmount_ShouldReturn400()
        {
            var (client, pid) = await SetupAsync();
            var r = await client.PostAsJsonAsync("/api/v1/incomes", new
            {
                periodId = pid,
                fortnightType = 1,
                description = "X",
                amount = -100,
                receivedAt = "2026-09-15"
            });
            r.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact(DisplayName = "DELETE /incomes/{id} deve retornar 204 e GET deve retornar 404")]
        public async Task Delete_ShouldReturn204AndGetReturns404()
        {
            var (client, pid) = await SetupAsync();

            var created = await client.PostAsJsonAsync("/api/v1/incomes", new
            {
                periodId = pid,
                fortnightType = 2,
                description = "Saldo MDS",
                amount = 2613.15,
                receivedAt = "2026-09-30"
            });
            var body = await created.Content.ReadFromJsonAsync<JsonElement>();
            var id = body.GetProperty("id").GetString()!;

            var del = await client.DeleteAsync($"/api/v1/incomes/{id}");
            del.StatusCode.Should().Be(HttpStatusCode.NoContent);

            var getById = await client.GetAsync($"/api/v1/incomes/{id}");
            getById.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact(DisplayName = "PUT /incomes/{id} deve retornar 204 e GET seguinte deve refletir os novos valores com o mesmo Id")]
        public async Task Update_ShouldReturn204AndGetReflectsNewValuesWithSameId()
        {
            var (client, pid) = await SetupAsync();

            var created = await client.PostAsJsonAsync("/api/v1/incomes", new
            {
                periodId = pid,
                fortnightType = 1,
                description = "Original",
                amount = 100.00,
                receivedAt = "2026-09-10"
            });
            var body = await created.Content.ReadFromJsonAsync<JsonElement>();
            var id = body.GetProperty("id").GetString()!;

            var r = await client.PutAsJsonAsync($"/api/v1/incomes/{id}", new
            {
                fortnightType = 2,
                description = "Atualizado",
                amount = 200.00,
                receivedAt = "2026-09-20",
                notes = "Ajuste"
            });
            r.StatusCode.Should().Be(HttpStatusCode.NoContent);

            var getById = await client.GetAsync($"/api/v1/incomes/{id}");
            getById.StatusCode.Should().Be(HttpStatusCode.OK);
            var inc = await getById.Content.ReadFromJsonAsync<JsonElement>();
            inc.GetProperty("id").GetString().Should().Be(id);
            inc.GetProperty("description").GetString().Should().Be("Atualizado");
            inc.GetProperty("amount").GetDecimal().Should().Be(200m);
            inc.GetProperty("fortnightType").GetInt32().Should().Be(2);
        }

        [Fact(DisplayName = "PUT /incomes/{id} deve retornar 400 para amount inválido")]
        public async Task Update_WithInvalidAmount_ShouldReturn400()
        {
            var (client, pid) = await SetupAsync();

            var created = await client.PostAsJsonAsync("/api/v1/incomes", new
            {
                periodId = pid,
                fortnightType = 1,
                description = "Original",
                amount = 100.00,
                receivedAt = "2026-09-10"
            });
            var body = await created.Content.ReadFromJsonAsync<JsonElement>();
            var id = body.GetProperty("id").GetString()!;

            var r = await client.PutAsJsonAsync($"/api/v1/incomes/{id}", new
            {
                fortnightType = 1,
                description = "Original",
                amount = -50.00,
                receivedAt = "2026-09-10"
            });
            r.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact(DisplayName = "PUT /incomes/{id} de outro usuário deve retornar 400")]
        public async Task Update_ForIncomeOfAnotherUser_ShouldReturn400()
        {
            var (client1, pid) = await SetupAsync();
            var (client2, _)   = await GetAuthenticatedClientAsync();

            var created = await client1.PostAsJsonAsync("/api/v1/incomes", new
            {
                periodId = pid,
                fortnightType = 1,
                description = "Privada",
                amount = 100.00,
                receivedAt = "2026-09-10"
            });
            var body = await created.Content.ReadFromJsonAsync<JsonElement>();
            var id = body.GetProperty("id").GetString()!;

            var r = await client2.PutAsJsonAsync($"/api/v1/incomes/{id}", new
            {
                fortnightType = 1,
                description = "Invadido",
                amount = 300.00,
                receivedAt = "2026-09-10"
            });
            r.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact(DisplayName = "PUT /incomes/{id} inexistente deve retornar 400")]
        public async Task Update_WithNonExistentId_ShouldReturn400()
        {
            var (client, _) = await SetupAsync();

            var r = await client.PutAsJsonAsync($"/api/v1/incomes/{Guid.NewGuid()}", new
            {
                fortnightType = 1,
                description = "X",
                amount = 100.00,
                receivedAt = "2026-09-10"
            });
            r.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact(DisplayName = "PUT /incomes/{id} em receita soft-deleted deve retornar 400")]
        public async Task Update_ForSoftDeletedIncome_ShouldReturn400()
        {
            var (client, pid) = await SetupAsync();

            var created = await client.PostAsJsonAsync("/api/v1/incomes", new
            {
                periodId = pid,
                fortnightType = 1,
                description = "Será excluída",
                amount = 100.00,
                receivedAt = "2026-09-10"
            });
            var body = await created.Content.ReadFromJsonAsync<JsonElement>();
            var id = body.GetProperty("id").GetString()!;

            var del = await client.DeleteAsync($"/api/v1/incomes/{id}");
            del.StatusCode.Should().Be(HttpStatusCode.NoContent);

            var r = await client.PutAsJsonAsync($"/api/v1/incomes/{id}", new
            {
                fortnightType = 1,
                description = "Não deveria funcionar",
                amount = 100.00,
                receivedAt = "2026-09-10"
            });
            r.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }
    }
}
