using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace PersonalFinance.Api.Tests.Integration
{
    public class ExpensesSortingControllerTests : ApiIntegrationTestBase
    {
        public ExpensesSortingControllerTests(TestWebApplicationFactory f) : base(f) { }

        private async Task<(HttpClient client, string periodId, string categoryId)> SetupAsync()
        {
            var (client, _) = await GetAuthenticatedClientAsync();

            var period = await client.PostAsJsonAsync("/api/v1/periods", new { year = 2026, month = 9 });
            var pBody = await period.Content.ReadFromJsonAsync<JsonElement>();
            var pid = pBody.GetProperty("id").GetString()!;

            var cat = await client.PostAsJsonAsync("/api/v1/categories", new
            { name = $"Cat_{Guid.NewGuid():N}", color = "#1E4D2B" });
            var cBody = await cat.Content.ReadFromJsonAsync<JsonElement>();
            var cid = cBody.GetProperty("id").GetString()!;

            return (client, pid, cid);
        }

        private static async Task<string> CreateExpenseAsync(
            HttpClient client, string periodId, string categoryId,
            string description, decimal amount, string dueDate,
            int sourceType = 2, int fortnightType = 1)
        {
            var r = await client.PostAsJsonAsync("/api/v1/expenses", new
            {
                periodId,
                categoryId,
                sourceType,
                fortnightType,
                description,
                amount,
                dueDate
            });
            r.StatusCode.Should().Be(HttpStatusCode.Created);
            var body = await r.Content.ReadFromJsonAsync<JsonElement>();
            return body.GetProperty("id").GetString()!;
        }

        // ── Description ──────────────────────────────────────────────────────────

        [Fact(DisplayName = "Sort por Description ASC deve retornar ordem alfabética crescente")]
        public async Task Sort_ByDescription_Ascending_ShouldReturnAlphaOrder()
        {
            var (client, pid, cid) = await SetupAsync();
            await CreateExpenseAsync(client, pid, cid, "Zeta", 100m, "2026-09-10");
            await CreateExpenseAsync(client, pid, cid, "Alpha", 200m, "2026-09-11");
            await CreateExpenseAsync(client, pid, cid, "Mango", 300m, "2026-09-12");

            // sortColumn=1 (Description), sortDirection=1 (Ascending)
            var r = await client.GetAsync($"/api/v1/expenses?periodId={pid}&sortColumn=1&sortDirection=1&pageSize=10");
            r.StatusCode.Should().Be(HttpStatusCode.OK);
            var items = (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("items");

            items[0].GetProperty("description").GetString().Should().Be("Alpha");
            items[1].GetProperty("description").GetString().Should().Be("Mango");
            items[2].GetProperty("description").GetString().Should().Be("Zeta");
        }

        [Fact(DisplayName = "Sort por Description DESC deve retornar ordem alfabética decrescente")]
        public async Task Sort_ByDescription_Descending_ShouldReturnReverseAlphaOrder()
        {
            var (client, pid, cid) = await SetupAsync();
            await CreateExpenseAsync(client, pid, cid, "Alpha", 100m, "2026-09-10");
            await CreateExpenseAsync(client, pid, cid, "Zeta", 200m, "2026-09-11");
            await CreateExpenseAsync(client, pid, cid, "Mango", 300m, "2026-09-12");

            // sortColumn=1 (Description), sortDirection=2 (Descending)
            var r = await client.GetAsync($"/api/v1/expenses?periodId={pid}&sortColumn=1&sortDirection=2&pageSize=10");
            r.StatusCode.Should().Be(HttpStatusCode.OK);
            var items = (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("items");

            items[0].GetProperty("description").GetString().Should().Be("Zeta");
            items[1].GetProperty("description").GetString().Should().Be("Mango");
            items[2].GetProperty("description").GetString().Should().Be("Alpha");
        }

        // ── Amount ────────────────────────────────────────────────────────────────

        [Fact(DisplayName = "Sort por Amount ASC deve retornar do menor para o maior")]
        public async Task Sort_ByAmount_Ascending_ShouldReturnAscendingAmounts()
        {
            var (client, pid, cid) = await SetupAsync();
            await CreateExpenseAsync(client, pid, cid, "C", 300m, "2026-09-10");
            await CreateExpenseAsync(client, pid, cid, "A", 100m, "2026-09-11");
            await CreateExpenseAsync(client, pid, cid, "B", 200m, "2026-09-12");

            // sortColumn=6 (Amount), sortDirection=1 (Ascending)
            var r = await client.GetAsync($"/api/v1/expenses?periodId={pid}&sortColumn=6&sortDirection=1&pageSize=10");
            r.StatusCode.Should().Be(HttpStatusCode.OK);
            var items = (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("items");

            items[0].GetProperty("amount").GetDecimal().Should().Be(100m);
            items[1].GetProperty("amount").GetDecimal().Should().Be(200m);
            items[2].GetProperty("amount").GetDecimal().Should().Be(300m);
        }

        [Fact(DisplayName = "Sort por Amount DESC deve retornar do maior para o menor")]
        public async Task Sort_ByAmount_Descending_ShouldReturnDescendingAmounts()
        {
            var (client, pid, cid) = await SetupAsync();
            await CreateExpenseAsync(client, pid, cid, "A", 100m, "2026-09-10");
            await CreateExpenseAsync(client, pid, cid, "B", 200m, "2026-09-11");
            await CreateExpenseAsync(client, pid, cid, "C", 300m, "2026-09-12");

            // sortColumn=6 (Amount), sortDirection=2 (Descending)
            var r = await client.GetAsync($"/api/v1/expenses?periodId={pid}&sortColumn=6&sortDirection=2&pageSize=10");
            r.StatusCode.Should().Be(HttpStatusCode.OK);
            var items = (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("items");

            items[0].GetProperty("amount").GetDecimal().Should().Be(300m);
            items[1].GetProperty("amount").GetDecimal().Should().Be(200m);
            items[2].GetProperty("amount").GetDecimal().Should().Be(100m);
        }

        // ── DueDate ───────────────────────────────────────────────────────────────

        [Fact(DisplayName = "Sort por DueDate ASC deve retornar da data mais antiga para a mais recente")]
        public async Task Sort_ByDueDate_Ascending_ShouldReturnOldestFirst()
        {
            var (client, pid, cid) = await SetupAsync();
            await CreateExpenseAsync(client, pid, cid, "A", 100m, "2026-09-20");
            await CreateExpenseAsync(client, pid, cid, "B", 100m, "2026-09-05");
            await CreateExpenseAsync(client, pid, cid, "C", 100m, "2026-09-12");

            // sortColumn=5 (DueDate), sortDirection=1 (Ascending)
            var r = await client.GetAsync($"/api/v1/expenses?periodId={pid}&sortColumn=5&sortDirection=1&pageSize=10");
            r.StatusCode.Should().Be(HttpStatusCode.OK);
            var items = (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("items");

            items[0].GetProperty("dueDate").GetString().Should().Be("2026-09-05");
            items[1].GetProperty("dueDate").GetString().Should().Be("2026-09-12");
            items[2].GetProperty("dueDate").GetString().Should().Be("2026-09-20");
        }

        [Fact(DisplayName = "Sort por DueDate DESC deve retornar da data mais recente para a mais antiga")]
        public async Task Sort_ByDueDate_Descending_ShouldReturnNewestFirst()
        {
            var (client, pid, cid) = await SetupAsync();
            await CreateExpenseAsync(client, pid, cid, "A", 100m, "2026-09-05");
            await CreateExpenseAsync(client, pid, cid, "B", 100m, "2026-09-20");
            await CreateExpenseAsync(client, pid, cid, "C", 100m, "2026-09-12");

            // sortColumn=5 (DueDate), sortDirection=2 (Descending)
            var r = await client.GetAsync($"/api/v1/expenses?periodId={pid}&sortColumn=5&sortDirection=2&pageSize=10");
            r.StatusCode.Should().Be(HttpStatusCode.OK);
            var items = (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("items");

            items[0].GetProperty("dueDate").GetString().Should().Be("2026-09-20");
            items[1].GetProperty("dueDate").GetString().Should().Be("2026-09-12");
            items[2].GetProperty("dueDate").GetString().Should().Be("2026-09-05");
        }

        // ── Status ────────────────────────────────────────────────────────────────

        [Fact(DisplayName = "Sort por Status ASC deve retornar na ordem crescente de enum (Pending < Paid < Cancelled)")]
        public async Task Sort_ByStatus_Ascending_ShouldReturnByEnumValueAsc()
        {
            var (client, pid, cid) = await SetupAsync();
            var cancelledId = await CreateExpenseAsync(client, pid, cid, "Cancelada", 100m, "2026-09-10");
            var paidId      = await CreateExpenseAsync(client, pid, cid, "Paga", 100m, "2026-09-11");
            var pendingId   = await CreateExpenseAsync(client, pid, cid, "Pendente", 100m, "2026-09-12");

            await client.PatchAsJsonAsync($"/api/v1/expenses/{cancelledId}/cancel", new { });
            await client.PatchAsJsonAsync($"/api/v1/expenses/{paidId}/pay",
                new { paymentDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)).ToString("yyyy-MM-dd") });

            // sortColumn=7 (Status), sortDirection=1 (Ascending)
            var r = await client.GetAsync($"/api/v1/expenses?periodId={pid}&sortColumn=7&sortDirection=1&pageSize=10");
            r.StatusCode.Should().Be(HttpStatusCode.OK);
            var items = (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("items");

            // Pending=1, Paid=2, Cancelled=3
            items[0].GetProperty("paymentStatus").GetInt32().Should().Be(1); // Pending
            items[1].GetProperty("paymentStatus").GetInt32().Should().Be(2); // Paid
            items[2].GetProperty("paymentStatus").GetInt32().Should().Be(3); // Cancelled
        }

        // ── Source ────────────────────────────────────────────────────────────────

        [Fact(DisplayName = "Sort por Source ASC deve ordenar por SourceType crescente (Parental < Personal)")]
        public async Task Sort_BySource_Ascending_ShouldReturnBySourceTypeAsc()
        {
            var (client, pid, cid) = await SetupAsync();
            await CreateExpenseAsync(client, pid, cid, "Personal", 100m, "2026-09-10", sourceType: 2);
            await CreateExpenseAsync(client, pid, cid, "Parental", 100m, "2026-09-11", sourceType: 1);

            // sortColumn=3 (Source), sortDirection=1 (Ascending)
            var r = await client.GetAsync($"/api/v1/expenses?periodId={pid}&sortColumn=3&sortDirection=1&pageSize=10");
            r.StatusCode.Should().Be(HttpStatusCode.OK);
            var items = (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("items");

            // Parental=1, Personal=2
            items[0].GetProperty("sourceType").GetInt32().Should().Be(1); // Parental
            items[1].GetProperty("sourceType").GetInt32().Should().Be(2); // Personal
        }

        // ── Fortnight ─────────────────────────────────────────────────────────────

        [Fact(DisplayName = "Sort por Fortnight ASC deve ordenar por FortnightType crescente (First < Second)")]
        public async Task Sort_ByFortnight_Ascending_ShouldReturnByFortnightAsc()
        {
            var (client, pid, cid) = await SetupAsync();
            await CreateExpenseAsync(client, pid, cid, "Segunda", 100m, "2026-09-20", fortnightType: 2);
            await CreateExpenseAsync(client, pid, cid, "Primeira", 100m, "2026-09-05", fortnightType: 1);

            // sortColumn=4 (Fortnight), sortDirection=1 (Ascending)
            var r = await client.GetAsync($"/api/v1/expenses?periodId={pid}&sortColumn=4&sortDirection=1&pageSize=10");
            r.StatusCode.Should().Be(HttpStatusCode.OK);
            var items = (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("items");

            items[0].GetProperty("fortnightType").GetInt32().Should().Be(1); // First
            items[1].GetProperty("fortnightType").GetInt32().Should().Be(2); // Second
        }

        // ── DragAndDropOrder ──────────────────────────────────────────────────────

        [Fact(DisplayName = "Sort por DragAndDropOrder deve respeitar a ordem salva pelo usuário")]
        public async Task Sort_ByDragAndDropOrder_ShouldRespectSavedOrder()
        {
            var (client, pid, cid) = await SetupAsync();
            var id1 = await CreateExpenseAsync(client, pid, cid, "Primeira", 100m, "2026-09-10");
            var id2 = await CreateExpenseAsync(client, pid, cid, "Segunda",  200m, "2026-09-11");
            var id3 = await CreateExpenseAsync(client, pid, cid, "Terceira", 300m, "2026-09-12");

            // Salva ordem invertida: id3=1, id2=2, id1=3
            await client.PostAsJsonAsync("/api/v1/expenses/order", new[]
            {
                new { expenseId = id3, order = 1 },
                new { expenseId = id2, order = 2 },
                new { expenseId = id1, order = 3 }
            });

            // sortColumn=8 (DragAndDropOrder), sortDirection=1 (Ascending)
            var r = await client.GetAsync($"/api/v1/expenses?periodId={pid}&sortColumn=8&sortDirection=1&pageSize=10");
            r.StatusCode.Should().Be(HttpStatusCode.OK);
            var items = (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("items");

            items[0].GetProperty("id").GetString().Should().Be(id3);
            items[1].GetProperty("id").GetString().Should().Be(id2);
            items[2].GetProperty("id").GetString().Should().Be(id1);
        }

        // ── UpdatedAt ─────────────────────────────────────────────────────────────

        [Fact(DisplayName = "Sort por UpdatedAt ASC deve retornar da mais antiga para a mais recente")]
        public async Task Sort_ByUpdatedAt_Ascending_ShouldReturnOldestFirst()
        {
            var (client, pid, cid) = await SetupAsync();
            var id1 = await CreateExpenseAsync(client, pid, cid, "Primeira", 100m, "2026-09-10");
            await Task.Delay(50);
            var id2 = await CreateExpenseAsync(client, pid, cid, "Segunda", 200m, "2026-09-11");
            await Task.Delay(50);
            await CreateExpenseAsync(client, pid, cid, "Terceira", 300m, "2026-09-12");

            // sortColumn=9 (UpdatedAt), sortDirection=1 (Ascending)
            var r = await client.GetAsync($"/api/v1/expenses?periodId={pid}&sortColumn=9&sortDirection=1&pageSize=10");
            r.StatusCode.Should().Be(HttpStatusCode.OK);
            var items = (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("items");

            items[0].GetProperty("id").GetString().Should().Be(id1);
        }

        [Fact(DisplayName = "Sort por UpdatedAt DESC deve retornar da mais recente para a mais antiga")]
        public async Task Sort_ByUpdatedAt_Descending_ShouldReturnNewestFirst()
        {
            var (client, pid, cid) = await SetupAsync();
            var id1 = await CreateExpenseAsync(client, pid, cid, "Primeira", 100m, "2026-09-10");
            await Task.Delay(50);
            await CreateExpenseAsync(client, pid, cid, "Segunda", 200m, "2026-09-11");
            await Task.Delay(50);
            var id3 = await CreateExpenseAsync(client, pid, cid, "Terceira", 300m, "2026-09-12");

            // sortColumn=9 (UpdatedAt), sortDirection=2 (Descending)
            var r = await client.GetAsync($"/api/v1/expenses?periodId={pid}&sortColumn=9&sortDirection=2&pageSize=10");
            r.StatusCode.Should().Be(HttpStatusCode.OK);
            var items = (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("items");

            items[0].GetProperty("id").GetString().Should().Be(id3);
            items[2].GetProperty("id").GetString().Should().Be(id1);
        }

        // ── Default (sem sort) ────────────────────────────────────────────────────

        [Fact(DisplayName = "Sem sortColumn deve manter comportamento padrão (DragAndDropOrder → FortnightType → DueDate)")]
        public async Task NoSort_ShouldUseDefaultOrder()
        {
            var (client, pid, cid) = await SetupAsync();
            var id1 = await CreateExpenseAsync(client, pid, cid, "A", 100m, "2026-09-15", fortnightType: 1);
            var id2 = await CreateExpenseAsync(client, pid, cid, "B", 200m, "2026-09-05", fortnightType: 1);
            var id3 = await CreateExpenseAsync(client, pid, cid, "C", 300m, "2026-09-20", fortnightType: 2);

            var r = await client.GetAsync($"/api/v1/expenses?periodId={pid}&pageSize=10");
            r.StatusCode.Should().Be(HttpStatusCode.OK);
            var items = (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("items");

            // Sem ordem salva: FortnightType(1) primeiro, depois DueDate crescente dentro de cada quinzena
            items[0].GetProperty("id").GetString().Should().Be(id2); // Fortnight=1, DueDate=05
            items[1].GetProperty("id").GetString().Should().Be(id1); // Fortnight=1, DueDate=15
            items[2].GetProperty("id").GetString().Should().Be(id3); // Fortnight=2, DueDate=20
        }
    }
}
