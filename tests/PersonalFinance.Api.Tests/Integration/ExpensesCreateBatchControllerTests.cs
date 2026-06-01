using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace PersonalFinance.Api.Tests.Integration
{
    public class ExpensesCreateBatchControllerTests : ApiIntegrationTestBase
    {
        public ExpensesCreateBatchControllerTests(TestWebApplicationFactory f) : base(f) { }

        private async Task<(HttpClient client, string periodId, string categoryId)> SetupAsync()
        {
            var (client, _) = await GetAuthenticatedClientAsync();

            var period = await client.PostAsJsonAsync("/api/v1/periods", new { year = 2026, month = 10 });
            var pBody  = await period.Content.ReadFromJsonAsync<JsonElement>();
            var pid    = pBody.GetProperty("id").GetString()!;

            var cat   = await client.PostAsJsonAsync("/api/v1/categories", new
            { name = $"CatCreateBatch_{Guid.NewGuid():N}", color = "#3A7BD5" });
            var cBody = await cat.Content.ReadFromJsonAsync<JsonElement>();
            var cid   = cBody.GetProperty("id").GetString()!;

            return (client, pid, cid);
        }

        [Fact(DisplayName = "POST /expenses/batch/create deve criar N despesas e retornar 201")]
        public async Task CreateBatch_ShouldReturn201WithAllExpenses()
        {
            var (client, pid, cid) = await SetupAsync();

            var payload = new
            {
                periodId = pid,
                items = new[]
                {
                    new { categoryId = cid, sourceType = 2, fortnightType = 1, description = "Lote 1", amount = 100.00, dueDate = "2026-10-05" },
                    new { categoryId = cid, sourceType = 2, fortnightType = 1, description = "Lote 2", amount = 200.00, dueDate = "2026-10-10" },
                    new { categoryId = cid, sourceType = 2, fortnightType = 1, description = "Lote 3", amount = 300.00, dueDate = "2026-10-15" }
                }
            };

            var r = await client.PostAsJsonAsync("/api/v1/expenses/batch/create", payload);

            r.StatusCode.Should().Be(HttpStatusCode.Created);

            var body = await r.Content.ReadFromJsonAsync<JsonElement>();
            body.GetArrayLength().Should().Be(3);

            var list = await client.GetAsync($"/api/v1/expenses?periodId={pid}");
            var listBody = await list.Content.ReadFromJsonAsync<JsonElement>();
            listBody.GetProperty("items").GetArrayLength().Should().Be(3);
        }

        [Fact(DisplayName = "POST /expenses/batch/create com lista vazia deve retornar 400")]
        public async Task CreateBatch_EmptyList_ShouldReturn400()
        {
            var (client, pid, _) = await SetupAsync();

            var r = await client.PostAsJsonAsync("/api/v1/expenses/batch/create", new
            {
                periodId = pid,
                items = new object[] { }
            });

            r.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact(DisplayName = "POST /expenses/batch/create com categoria inválida deve retornar 400 sem persistir")]
        public async Task CreateBatch_InvalidCategory_ShouldReturn400WithRollback()
        {
            var (client, pid, cid) = await SetupAsync();
            var invalidCatId = Guid.NewGuid().ToString();

            var payload = new
            {
                periodId = pid,
                items = new[]
                {
                    new { categoryId = cid,           sourceType = 2, fortnightType = 1, description = "Válida",   amount = 100.00, dueDate = "2026-10-05" },
                    new { categoryId = invalidCatId,  sourceType = 2, fortnightType = 1, description = "Inválida", amount = 200.00, dueDate = "2026-10-10" }
                }
            };

            var r = await client.PostAsJsonAsync("/api/v1/expenses/batch/create", payload);

            r.StatusCode.Should().Be(HttpStatusCode.BadRequest);

            // Nenhuma despesa deve ter sido persistida (rollback)
            var list = await client.GetAsync($"/api/v1/expenses?periodId={pid}");
            var listBody = await list.Content.ReadFromJsonAsync<JsonElement>();
            listBody.GetProperty("items").GetArrayLength().Should().Be(0);
        }
    }
}
