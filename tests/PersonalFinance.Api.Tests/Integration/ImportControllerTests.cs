using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace PersonalFinance.Api.Tests.Integration;

public class ImportControllerTests : ApiIntegrationTestBase
{
    public ImportControllerTests(TestWebApplicationFactory factory) : base(factory) { }

    // ── Autenticação ──────────────────────────────────────────────────────────

    [Fact(DisplayName = "POST /import/legacy sem token deve retornar 401")]
    public async Task Import_WithoutToken_ShouldReturn401()
    {
        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(new byte[] { 1, 2, 3 }), "file", "test.xlsx");

        var r = await Client.PostAsync("/api/v1/import/legacy", content);

        r.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── Validação de arquivo ──────────────────────────────────────────────────

    [Fact(DisplayName = "POST /import/legacy sem arquivo deve retornar 400")]
    public async Task Import_WithoutFile_ShouldReturn400()
    {
        var (client, _) = await GetAuthenticatedClientAsync();

        using var content = new MultipartFormDataContent();
        var r = await client.PostAsync("/api/v1/import/legacy", content);

        r.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "POST /import/legacy com arquivo .csv deve retornar 400")]
    public async Task Import_WithCsvFile_ShouldReturn400()
    {
        var (client, _) = await GetAuthenticatedClientAsync();

        using var content = new MultipartFormDataContent();
        content.Add(
            new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes("col1,col2")),
            "file", "dados.csv");

        var r = await client.PostAsync("/api/v1/import/legacy", content);

        r.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await r.Content.ReadAsStringAsync();
        body.Should().Contain("xlsx");
    }

    [Fact(DisplayName = "POST /import/legacy com arquivo xlsx vazio deve retornar 400")]
    public async Task Import_WithEmptyFile_ShouldReturn400()
    {
        var (client, _) = await GetAuthenticatedClientAsync();

        using var content = new MultipartFormDataContent();
        content.Add(
            new ByteArrayContent(Array.Empty<byte>()),
            "file", "vazio.xlsx");

        var r = await client.PostAsync("/api/v1/import/legacy", content);

        r.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── Importação real com planilha mínima ───────────────────────────────────

    [Fact(DisplayName = "POST /import/legacy com xlsx válido deve retornar 200 com sumário")]
    public async Task Import_WithValidXlsx_ShouldReturn200WithSummary()
    {
        var (client, _) = await GetAuthenticatedClientAsync();

        // Cria um xlsx mínimo em memória com estrutura válida
        var xlsxBytes = BuildMinimalWorkbook();

        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(xlsxBytes), "file", "planilha.xlsx");

        var r = await client.PostAsync("/api/v1/import/legacy", content);

        r.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await r.Content.ReadFromJsonAsync<JsonElement>();
        body.TryGetProperty("periodsCreated",    out _).Should().BeTrue();
        body.TryGetProperty("expensesImported",  out _).Should().BeTrue();
        body.TryGetProperty("incomesImported",   out _).Should().BeTrue();
        body.TryGetProperty("warnings",          out _).Should().BeTrue();
    }

    [Fact(DisplayName = "Segunda importação do mesmo arquivo deve retornar períodos pulados")]
    public async Task Import_Twice_SecondShouldSkipExistingPeriods()
    {
        var (client, _) = await GetAuthenticatedClientAsync();
        var xlsxBytes   = BuildMinimalWorkbook();

        // Primeira importação
        using var content1 = new MultipartFormDataContent();
        content1.Add(new ByteArrayContent(xlsxBytes), "file", "planilha.xlsx");
        await client.PostAsync("/api/v1/import/legacy", content1);

        // Segunda importação — deve pular períodos já existentes
        using var content2 = new MultipartFormDataContent();
        content2.Add(new ByteArrayContent(xlsxBytes), "file", "planilha.xlsx");
        var r2 = await client.PostAsync("/api/v1/import/legacy", content2);

        r2.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await r2.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("periodsCreated").GetInt32().Should().Be(0);
        body.GetProperty("periodsSkipped").GetInt32().Should().BeGreaterThan(0);
    }

    // ── Helper: workbook mínimo em memória ───────────────────────────────────

    private static byte[] BuildMinimalWorkbook()
    {
        using var wb = new ClosedXML.Excel.XLWorkbook();
        var ws = wb.AddWorksheet("Abril2026");

        // Receita primária na B4
        ws.Cell("B4").FormulaA1 =
            "=SUM(2613.15)+sum(Q7:Q50)-sum(P7:P50)";
        ws.Cell("C4").FormulaA1 =
            "=5500+sum(Q51:Q100)-sum(P51:P100)";

        // Cabeçalho
        ws.Cell(5, 1).Value = "Fonte";
        ws.Cell(5, 2).Value = "Dívida";
        ws.Cell(5, 3).Value = "Valor";
        ws.Cell(5, 4).Value = "Quinzena";
        ws.Cell(5, 5).Value = "Pago?";

        // Uma despesa
        ws.Cell(6, 1).Value = "Despesa Própria";
        ws.Cell(6, 2).Value = "Internet (venc. 20/04)";
        ws.Cell(6, 3).Value = 110.99;
        ws.Cell(6, 4).Value = 1.0;
        ws.Cell(6, 5).Value = "NÃO";

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }
}
