using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace PersonalFinance.Api.Tests.Integration;

/// <summary>
/// Testes de integração do PurgeController.
/// O controller ainda não existe — os testes devem falhar por comportamento ausente (404),
/// não por erro de compilação.
/// </summary>
public class PurgeControllerTests : ApiIntegrationTestBase
{
    public PurgeControllerTests(TestWebApplicationFactory f) : base(f) { }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Cria um período inativo (inactive) para usar nos testes de expurgo.
    /// Um período inativo é criado e depois tem o toggle-active chamado para desativá-lo.
    /// </summary>
    private async Task<(HttpClient client, string periodId)> SetupInactivePeriodAsync(int month = 3)
    {
        var (client, _) = await GetAuthenticatedClientAsync();

        var period = await client.PostAsJsonAsync("/api/v1/periods",
            new { year = 2025, month });
        period.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await period.Content.ReadFromJsonAsync<JsonElement>();
        var periodId = body.GetProperty("id").GetString()!;

        // Desativa o período para torná-lo elegível ao expurgo
        await client.PatchAsync($"/api/v1/periods/{periodId}/toggle-active", null);

        return (client, periodId);
    }

    // ── Export ────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "GET /purge/{periodId}/export deve retornar 200 com Content-Type text/csv")]
    public async Task Export_ValidPeriod_ShouldReturn200WithCsvContentType()
    {
        var (client, periodId) = await SetupInactivePeriodAsync(month: 3);

        var r = await client.GetAsync($"/api/v1/purge/{periodId}/export");

        r.StatusCode.Should().Be(HttpStatusCode.OK);
        r.Content.Headers.ContentType!.MediaType.Should().Be("text/csv");
        var body = await r.Content.ReadAsByteArrayAsync();
        body.Should().NotBeEmpty();
    }

    [Fact(DisplayName = "GET /purge/{periodId}/export de período inexistente deve retornar 404")]
    public async Task Export_NonExistentPeriod_ShouldReturn404()
    {
        var (client, _) = await GetAuthenticatedClientAsync();
        var fakeId = Guid.NewGuid();

        var r = await client.GetAsync($"/api/v1/purge/{fakeId}/export");

        r.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── Purge ─────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "POST /purge/{periodId} deve retornar 204 e criar PurgeRecord")]
    public async Task Purge_ValidPeriod_ShouldReturn204AndCreateRecord()
    {
        var (client, periodId) = await SetupInactivePeriodAsync(month: 4);

        // Executa expurgo
        var r = await client.PostAsJsonAsync($"/api/v1/purge/{periodId}",
            new { csvFileName = "pf_2025_04_expurgo.csv" });

        r.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Após expurgo, deve haver 1 registro em /purge/records
        var records = await client.GetAsync("/api/v1/purge/records");
        records.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await records.Content.ReadFromJsonAsync<JsonElement>();
        body.GetArrayLength().Should().BeGreaterThanOrEqualTo(1);
    }

    // ── GET Records ───────────────────────────────────────────────────────────

    [Fact(DisplayName = "GET /purge/records deve retornar 200 com lista de records do usuário")]
    public async Task GetRecords_ShouldReturn200WithUserRecords()
    {
        var (client, _) = await GetAuthenticatedClientAsync();

        var r = await client.GetAsync("/api/v1/purge/records");

        r.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await r.Content.ReadFromJsonAsync<JsonElement>();
        body.ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact(DisplayName = "GET /purge/records retorna apenas records do usuário autenticado")]
    public async Task GetRecords_ShouldReturnOnlyAuthenticatedUserRecords()
    {
        // Usuário 1: cria período inativo e faz expurgo
        var (client1, periodId) = await SetupInactivePeriodAsync(month: 5);
        await client1.PostAsJsonAsync($"/api/v1/purge/{periodId}",
            new { csvFileName = "pf_2025_05_expurgo.csv" });

        // Usuário 2: não tem records
        var (client2, _) = await GetAuthenticatedClientAsync();

        var r = await client2.GetAsync("/api/v1/purge/records");
        r.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await r.Content.ReadFromJsonAsync<JsonElement>();
        body.GetArrayLength().Should().Be(0);
    }

    // ── DELETE Record ─────────────────────────────────────────────────────────

    [Fact(DisplayName = "DELETE /purge/records/{id} deve retornar 204 e remover o registro")]
    public async Task DeleteRecord_ValidId_ShouldReturn204AndRemoveRecord()
    {
        var (client, periodId) = await SetupInactivePeriodAsync(month: 6);

        // Cria um PurgeRecord via expurgo
        await client.PostAsJsonAsync($"/api/v1/purge/{periodId}",
            new { csvFileName = "pf_2025_06_expurgo.csv" });

        // Obtém o ID do record criado
        var listResponse = await client.GetAsync("/api/v1/purge/records");
        var records = await listResponse.Content.ReadFromJsonAsync<JsonElement>();
        var recordId = records[0].GetProperty("id").GetString()!;

        // Deleta o record
        var deleteResponse = await client.DeleteAsync($"/api/v1/purge/records/{recordId}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Confirma remoção
        var afterDelete = await client.GetAsync("/api/v1/purge/records");
        var afterBody = await afterDelete.Content.ReadFromJsonAsync<JsonElement>();
        afterBody.GetArrayLength().Should().Be(0);
    }

    [Fact(DisplayName = "DELETE /purge/records/{id} de outro usuário deve retornar 404")]
    public async Task DeleteRecord_FromOtherUser_ShouldReturn404()
    {
        // Usuário 1: cria um record
        var (client1, periodId) = await SetupInactivePeriodAsync(month: 7);
        await client1.PostAsJsonAsync($"/api/v1/purge/{periodId}",
            new { csvFileName = "pf_2025_07_expurgo.csv" });

        var listResponse = await client1.GetAsync("/api/v1/purge/records");
        var records = await listResponse.Content.ReadFromJsonAsync<JsonElement>();
        var recordId = records[0].GetProperty("id").GetString()!;

        // Usuário 2: tenta deletar record do usuário 1
        var (client2, _) = await GetAuthenticatedClientAsync();
        var r = await client2.DeleteAsync($"/api/v1/purge/records/{recordId}");

        r.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── Eligible Periods ──────────────────────────────────────────────────────

    [Fact(DisplayName = "GET /purge/eligible-periods deve retornar 200 com lista")]
    public async Task GetEligiblePeriods_ShouldReturn200WithList()
    {
        var (client, _) = await SetupInactivePeriodAsync(month: 8);

        var r = await client.GetAsync("/api/v1/purge/eligible-periods");

        r.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await r.Content.ReadFromJsonAsync<JsonElement>();
        body.ValueKind.Should().Be(JsonValueKind.Array);
    }

    // ── RED: Bug 1 — periodId ausente no payload ──────────────────────────────

    [Fact(DisplayName = "RED: GET /purge/eligible-periods deve retornar campo 'periodId', não 'id'")]
    public async Task GetEligiblePeriods_ReturnsPeriodId_NotId()
    {
        // Arrange
        var (client, _) = await SetupInactivePeriodAsync(month: 9);

        // Act
        var r = await client.GetAsync("/api/v1/purge/eligible-periods");

        // Assert
        r.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await r.Content.ReadFromJsonAsync<JsonElement>();
        body.ValueKind.Should().Be(JsonValueKind.Array);
        body.GetArrayLength().Should().BeGreaterThan(0);

        var first = body[0];
        first.TryGetProperty("periodId", out _).Should().BeTrue("o payload deve conter 'periodId'");
        first.TryGetProperty("id", out _).Should().BeFalse("o campo 'id' não deve ser exposto diretamente");
    }

    [Fact(DisplayName = "RED: GET /purge/eligible-periods deve retornar totalIncome, totalExpense e itemCount")]
    public async Task GetEligiblePeriods_ReturnsTotals()
    {
        // Arrange
        var (client, _) = await SetupInactivePeriodAsync(month: 10);

        // Act
        var r = await client.GetAsync("/api/v1/purge/eligible-periods");

        // Assert
        r.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await r.Content.ReadFromJsonAsync<JsonElement>();
        body.GetArrayLength().Should().BeGreaterThan(0);

        var first = body[0];
        first.TryGetProperty("totalIncome", out _).Should().BeTrue("o payload deve conter 'totalIncome'");
        first.TryGetProperty("totalExpense", out _).Should().BeTrue("o payload deve conter 'totalExpense'");
        first.TryGetProperty("itemCount", out _).Should().BeTrue("o payload deve conter 'itemCount'");
    }

    // ── RED: Bug 2 — ExportPeriod declarado como POST mas deve ser GET ─────────

    [Fact(DisplayName = "RED: GET /purge/{periodId}/export deve responder a GET (não retornar 405)")]
    public async Task ExportPeriod_AcceptsGetRequest()
    {
        // Arrange
        var (client, periodId) = await SetupInactivePeriodAsync(month: 11);

        // Act — Angular usa GET /purge/{periodId}/export mas o endpoint é POST
        var r = await client.GetAsync($"/api/v1/purge/{periodId}/export");

        // Assert
        ((int)r.StatusCode).Should().NotBe(405, "o endpoint export deve aceitar GET, não apenas POST");
        r.StatusCode.Should().Be(HttpStatusCode.OK);
        r.Content.Headers.ContentType!.MediaType.Should().Be("text/csv");
    }
}
