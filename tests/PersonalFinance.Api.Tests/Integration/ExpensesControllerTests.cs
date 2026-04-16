using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace PersonalFinance.Api.Tests.Integration
{
    public class ExpensesControllerTests : ApiIntegrationTestBase
    {
        public ExpensesControllerTests(TestWebApplicationFactory f) : base(f) { }

        private async Task<(HttpClient client, string periodId, string categoryId)> SetupAsync()
        {
            var (client, _) = await GetAuthenticatedClientAsync();

            var period = await client.PostAsJsonAsync("/api/v1/periods", new { year = 2026, month = 8 });
            var pBody = await period.Content.ReadFromJsonAsync<JsonElement>();
            var pid = pBody.GetProperty("id").GetString()!;

            var cat = await client.PostAsJsonAsync("/api/v1/categories", new
            { name = $"Cat_{Guid.NewGuid():N}", color = "#1E4D2B" });
            var cBody = await cat.Content.ReadFromJsonAsync<JsonElement>();
            var cid = cBody.GetProperty("id").GetString()!;

            return (client, pid, cid);
        }

        [Fact(DisplayName = "GET /expenses?periodId deve retornar 200 com lista")]
        public async Task GetByPeriod_ShouldReturn200()
        {
            var (client, pid, _) = await SetupAsync();
            var r = await client.GetAsync($"/api/v1/expenses?periodId={pid}");
            r.StatusCode.Should().Be(HttpStatusCode.OK);
            var body = await r.Content.ReadFromJsonAsync<JsonElement>();
            body.GetProperty("items").GetArrayLength().Should().Be(0); // período vazio
        }

        [Fact(DisplayName = "POST e GET /expenses deve persistir e retornar a despesa")]
        public async Task CreateAndGet_ShouldPersistAndReturn()
        {
            var (client, pid, cid) = await SetupAsync();

            var created = await client.PostAsJsonAsync("/api/v1/expenses", new
            {
                periodId = pid,
                categoryId = cid,
                sourceType = 2,
                fortnightType = 1,
                description = "Aluguel",
                amount = 1200.00,
                dueDate = "2026-08-10"
            });
            created.StatusCode.Should().Be(HttpStatusCode.Created);
            var body = await created.Content.ReadFromJsonAsync<JsonElement>();
            var id = body.GetProperty("id").GetString()!;

            // GET /{id}
            var getById = await client.GetAsync($"/api/v1/expenses/{id}");
            getById.StatusCode.Should().Be(HttpStatusCode.OK);
            var exp = await getById.Content.ReadFromJsonAsync<JsonElement>();
            exp.GetProperty("description").GetString().Should().Be("Aluguel");
            exp.GetProperty("amount").GetDecimal().Should().Be(1200m);

            // GET lista — deve conter a despesa
            var list = await client.GetAsync($"/api/v1/expenses?periodId={pid}");
            var arr = await list.Content.ReadFromJsonAsync<JsonElement>();
            arr.GetProperty("items").GetArrayLength().Should().Be(1);
        }

        [Fact(DisplayName = "POST /expenses deve retornar 400 para amount zero")]
        public async Task Create_WithZeroAmount_ShouldReturn400()
        {
            var (client, pid, cid) = await SetupAsync();
            var r = await client.PostAsJsonAsync("/api/v1/expenses", new
            {
                periodId = pid,
                categoryId = cid,
                sourceType = 2,
                fortnightType = 1,
                description = "X",
                amount = 0,
                dueDate = "2026-08-10"
            });
            r.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact(DisplayName = "PATCH /expenses/{id}/pay deve retornar 204 e GET deve refletir status Paid")]
        public async Task MarkAsPaid_ShouldUpdateStatus()
        {
            var (client, pid, cid) = await SetupAsync();

            var created = await client.PostAsJsonAsync("/api/v1/expenses", new
            {
                periodId = pid,
                categoryId = cid,
                sourceType = 2,
                fortnightType = 1,
                description = "Internet",
                amount = 110.99,
                dueDate = "2026-08-20"
            });
            var body = await created.Content.ReadFromJsonAsync<JsonElement>();
            var id = body.GetProperty("id").GetString()!;

            var patch = await client.PatchAsJsonAsync($"/api/v1/expenses/{id}/pay",
                new { paymentDate = DateTime.Now.ToString("yyyy-MM-dd") });
            patch.StatusCode.Should().Be(HttpStatusCode.NoContent);

            var getById = await client.GetAsync($"/api/v1/expenses/{id}");
            var exp = await getById.Content.ReadFromJsonAsync<JsonElement>();
            exp.GetProperty("paymentStatus").GetInt32().Should().Be(2); // Paid = 2
        }

        [Fact(DisplayName = "PUT /expenses/{id} deve retornar 204")]
        public async Task Update_ShouldReturn204()
        {
            var (client, pid, cid) = await SetupAsync();

            var created = await client.PostAsJsonAsync("/api/v1/expenses", new
            {
                periodId = pid,
                categoryId = cid,
                sourceType = 2,
                fortnightType = 1,
                description = "Original",
                amount = 100.00,
                dueDate = "2026-08-10"
            });
            var body = await created.Content.ReadFromJsonAsync<JsonElement>();
            var id = body.GetProperty("id").GetString()!;

            var r = await client.PutAsJsonAsync($"/api/v1/expenses/{id}", new
            {
                categoryId = cid,
                sourceType = 2,
                fortnightType = 1,
                description = "Atualizado",
                amount = 200.00,
                dueDate = "2026-08-15",
                paymentStatus = 1
            });

            r.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        [Fact(DisplayName = "PATCH /expenses/{id}/cancel deve retornar 204")]
        public async Task Cancel_ShouldReturn204()
        {
            var (client, pid, cid) = await SetupAsync();

            var created = await client.PostAsJsonAsync("/api/v1/expenses", new
            {
                periodId = pid,
                categoryId = cid,
                sourceType = 2,
                fortnightType = 1,
                description = "A Cancelar",
                amount = 50.00,
                dueDate = "2026-08-10"
            });
            var body = await created.Content.ReadFromJsonAsync<JsonElement>();
            var id = body.GetProperty("id").GetString()!;

            var r = await client.PatchAsync($"/api/v1/expenses/{id}/cancel", null);

            r.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        [Fact(DisplayName = "PATCH /expenses/{id}/partial deve retornar 204")]
        public async Task MarkAsPartial_ShouldReturn204()
        {
            var (client, pid, cid) = await SetupAsync();

            var created = await client.PostAsJsonAsync("/api/v1/expenses", new
            {
                periodId = pid,
                categoryId = cid,
                sourceType = 2,
                fortnightType = 1,
                description = "Parcial",
                amount = 300.00,
                dueDate = "2026-08-10"
            });
            var body = await created.Content.ReadFromJsonAsync<JsonElement>();
            var id = body.GetProperty("id").GetString()!;

            var r = await client.PatchAsync($"/api/v1/expenses/{id}/partial", null);

            r.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        [Fact(DisplayName = "DELETE /expenses/{id} deve retornar 204 e sumir da lista")]
        public async Task Delete_ShouldReturn204AndDisappearFromList()
        {
            var (client, pid, cid) = await SetupAsync();

            var created = await client.PostAsJsonAsync("/api/v1/expenses", new
            {
                periodId = pid,
                categoryId = cid,
                sourceType = 2,
                fortnightType = 1,
                description = "Gás",
                amount = 50,
                dueDate = "2026-08-05"
            });
            var body = await created.Content.ReadFromJsonAsync<JsonElement>();
            var id = body.GetProperty("id").GetString()!;

            var del = await client.DeleteAsync($"/api/v1/expenses/{id}");
            del.StatusCode.Should().Be(HttpStatusCode.NoContent);

            // Após soft delete, GET /{id} deve retornar 404
            var getById = await client.GetAsync($"/api/v1/expenses/{id}");
            getById.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
    }
}
