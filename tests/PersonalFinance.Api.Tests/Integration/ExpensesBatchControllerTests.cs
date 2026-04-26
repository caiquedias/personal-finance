using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace PersonalFinance.Api.Tests.Integration
{
    public class ExpensesBatchControllerTests : ApiIntegrationTestBase
    {
        public ExpensesBatchControllerTests(TestWebApplicationFactory f) : base(f) { }

        private async Task<(HttpClient client, string periodId, string categoryId)> SetupAsync()
        {
            var (client, _) = await GetAuthenticatedClientAsync();

            var period = await client.PostAsJsonAsync("/api/v1/periods", new { year = 2026, month = 9 });
            var pBody  = await period.Content.ReadFromJsonAsync<JsonElement>();
            var pid    = pBody.GetProperty("id").GetString()!;

            var cat   = await client.PostAsJsonAsync("/api/v1/categories", new
            { name = $"CatBatch_{Guid.NewGuid():N}", color = "#1E4D2B" });
            var cBody = await cat.Content.ReadFromJsonAsync<JsonElement>();
            var cid   = cBody.GetProperty("id").GetString()!;

            return (client, pid, cid);
        }

        private async Task<string> CreateExpenseAsync(
            HttpClient client, string pid, string cid, string description)
        {
            var r = await client.PostAsJsonAsync("/api/v1/expenses", new
            {
                periodId = pid, categoryId = cid, sourceType = 2, fortnightType = 1,
                description, amount = 50.00, dueDate = "2026-09-10"
            });
            var body = await r.Content.ReadFromJsonAsync<JsonElement>();
            return body.GetProperty("id").GetString()!;
        }

        [Fact(DisplayName = "DELETE /expenses/batch deve soft-deletar todos os itens e retornar 204")]
        public async Task DeleteBatch_ShouldReturn204AndRemoveFromList()
        {
            var (client, pid, cid) = await SetupAsync();
            var id1 = await CreateExpenseAsync(client, pid, cid, "Batch Del 1");
            var id2 = await CreateExpenseAsync(client, pid, cid, "Batch Del 2");

            var r = await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/v1/expenses/batch")
            {
                Content = JsonContent.Create(new { expenseIds = new[] { id1, id2 } })
            });

            r.StatusCode.Should().Be(HttpStatusCode.NoContent);

            var list = await client.GetAsync($"/api/v1/expenses?periodId={pid}");
            var body = await list.Content.ReadFromJsonAsync<JsonElement>();
            body.GetProperty("items").GetArrayLength().Should().Be(0);
        }

        [Fact(DisplayName = "PATCH /expenses/batch/pay deve marcar todos como Paid e retornar 204")]
        public async Task PayBatch_ShouldReturn204AndStatusPaid()
        {
            var (client, pid, cid) = await SetupAsync();
            var id1 = await CreateExpenseAsync(client, pid, cid, "Batch Pay 1");
            var id2 = await CreateExpenseAsync(client, pid, cid, "Batch Pay 2");

            var r = await client.PatchAsJsonAsync("/api/v1/expenses/batch/pay",
                new { expenseIds = new[] { id1, id2 } });

            r.StatusCode.Should().Be(HttpStatusCode.NoContent);

            var e1 = await (await client.GetAsync($"/api/v1/expenses/{id1}"))
                .Content.ReadFromJsonAsync<JsonElement>();
            e1.GetProperty("paymentStatus").GetInt32().Should().Be(2); // Paid = 2

            var e2 = await (await client.GetAsync($"/api/v1/expenses/{id2}"))
                .Content.ReadFromJsonAsync<JsonElement>();
            e2.GetProperty("paymentStatus").GetInt32().Should().Be(2);
        }

        [Fact(DisplayName = "PATCH /expenses/batch/cancel deve marcar todos como Cancelled e retornar 204")]
        public async Task CancelBatch_ShouldReturn204AndStatusCancelled()
        {
            var (client, pid, cid) = await SetupAsync();
            var id1 = await CreateExpenseAsync(client, pid, cid, "Batch Cancel 1");
            var id2 = await CreateExpenseAsync(client, pid, cid, "Batch Cancel 2");

            var r = await client.PatchAsJsonAsync("/api/v1/expenses/batch/cancel",
                new { expenseIds = new[] { id1, id2 } });

            r.StatusCode.Should().Be(HttpStatusCode.NoContent);

            var e1 = await (await client.GetAsync($"/api/v1/expenses/{id1}"))
                .Content.ReadFromJsonAsync<JsonElement>();
            e1.GetProperty("paymentStatus").GetInt32().Should().Be(3); // Cancelled = 3

            var e2 = await (await client.GetAsync($"/api/v1/expenses/{id2}"))
                .Content.ReadFromJsonAsync<JsonElement>();
            e2.GetProperty("paymentStatus").GetInt32().Should().Be(3);
        }

        [Fact(DisplayName = "DELETE /expenses/batch com lista vazia deve retornar 400")]
        public async Task DeleteBatch_EmptyList_ShouldReturn400()
        {
            var (client, _, _) = await SetupAsync();
            var r = await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/v1/expenses/batch")
            {
                Content = JsonContent.Create(new { expenseIds = Array.Empty<string>() })
            });
            r.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact(DisplayName = "PATCH /expenses/batch/pay com lista vazia deve retornar 400")]
        public async Task PayBatch_EmptyList_ShouldReturn400()
        {
            var (client, _, _) = await SetupAsync();
            var r = await client.PatchAsJsonAsync("/api/v1/expenses/batch/pay",
                new { expenseIds = Array.Empty<string>() });
            r.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact(DisplayName = "PATCH /expenses/batch/cancel com lista vazia deve retornar 400")]
        public async Task CancelBatch_EmptyList_ShouldReturn400()
        {
            var (client, _, _) = await SetupAsync();
            var r = await client.PatchAsJsonAsync("/api/v1/expenses/batch/cancel",
                new { expenseIds = Array.Empty<string>() });
            r.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }
    }
}
