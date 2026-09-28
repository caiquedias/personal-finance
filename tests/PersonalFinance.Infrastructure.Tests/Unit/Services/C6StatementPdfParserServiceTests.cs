using FluentAssertions;
using PersonalFinance.Domain.Exceptions;
using PersonalFinance.Infrastructure.Services;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;
using Xunit;

namespace PersonalFinance.Infrastructure.Tests.Unit.Services;

/// <summary>
/// Testes unitários do C6StatementPdfParserService.
/// Geram PDFs sintéticos em memória (nunca arquivo real do extrato).
/// Layout assumido: cabeçalho de período + cabeçalho de colunas + linhas por faixa de X.
/// </summary>
public class C6StatementPdfParserServiceTests
{
    private readonly C6StatementPdfParserService _sut = new();

    // Faixas de X das colunas
    private const double XEvent = 40, XPosting = 110, XType = 180, XDesc = 260, XAmount = 480;

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Palavra posicionada: (texto, x, y).</summary>
    private sealed record Cell(string Text, double X, double Y);

    private static Cell[] Row(double y, string ev, string posting, string type, string desc, string amount) =>
        new[]
        {
            new Cell(ev, XEvent, y), new Cell(posting, XPosting, y), new Cell(type, XType, y),
            new Cell(desc, XDesc, y), new Cell(amount, XAmount, y)
        };

    private static Cell[] ColumnHeader(double y) =>
        Row(y, "Data lançamento", "Data contábil", "Tipo", "Descrição", "Valor");

    private static Cell[] PeriodHeader(string period) =>
        new[] { new Cell($"Extrato período: {period}", XEvent, 800) };

    private static MemoryStream BuildPdf(params Cell[][] pages)
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        foreach (var cells in pages)
        {
            var page = builder.AddPage(595, 842);
            foreach (var c in cells)
                page.AddText(StripDiacritics(c.Text), 9, new PdfPoint(c.X, c.Y), font);
        }
        return new MemoryStream(builder.Build());
    }

    // Fonte Standard14 do PdfPig não possui glifos acentuados; remove diacríticos só para gerar o PDF
    private static string StripDiacritics(string text) =>
        new string(text.Normalize(System.Text.NormalizationForm.FormD)
            .Where(ch => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch)
                         != System.Globalization.UnicodeCategory.NonSpacingMark)
            .ToArray());

    private static MemoryStream Encrypt(MemoryStream plain, string userPassword)
    {
        plain.Position = 0;
        using var doc = PdfReader.Open(plain, PdfDocumentOpenMode.Modify);
        doc.SecurityHandler.SetEncryptionToV2With128Bits();
        doc.SecuritySettings.UserPassword = userPassword;
        doc.SecuritySettings.OwnerPassword = userPassword + "-owner";
        var output = new MemoryStream();
        doc.Save(output, false);
        output.Position = 0;
        return output;
    }

    private static Cell[] SimplePage(string period, params Cell[][] rows) =>
        PeriodHeader(period).Concat(ColumnHeader(760)).Concat(rows.SelectMany(r => r)).ToArray();

    // ── Caminho feliz ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ParseAsync_DebitRow_ReturnsNegativeAmount()
    {
        using var pdf = BuildPdf(SimplePage("01/03/2025 a 31/03/2025",
            Row(730, "05/03", "06/03", "Compra", "MERCADO CENTRAL LTDA", "-R$ 89,90")));

        var result = await _sut.ParseAsync(pdf, null);

        result.Should().ContainSingle();
        var e = result[0];
        e.EventDate.Should().Be(new DateOnly(2025, 3, 5));
        e.PostingDate.Should().Be(new DateOnly(2025, 3, 6));
        e.RawType.Should().Be("Compra");
        e.Description.Should().Be("MERCADO CENTRAL LTDA");
        e.Amount.Should().Be(-89.90m);
    }

    [Fact]
    public async Task ParseAsync_CreditRow_ReturnsPositiveAmount()
    {
        using var pdf = BuildPdf(SimplePage("01/03/2025 a 31/03/2025",
            Row(730, "10/03", "10/03", "Pix", "SALARIO EMPRESA XYZ", "R$ 5.000,00")));

        var result = await _sut.ParseAsync(pdf, null);

        result.Should().ContainSingle();
        result[0].Amount.Should().Be(5000.00m);
        result[0].RawType.Should().Be("Pix");
    }

    [Fact]
    public async Task ParseAsync_ThousandsSeparator_ParsesPtBrAmount()
    {
        using var pdf = BuildPdf(SimplePage("01/03/2025 a 31/03/2025",
            Row(730, "05/03", "05/03", "Boleto", "ALUGUEL", "-R$ 1.234,56"),
            Row(710, "06/03", "06/03", "Pix", "REEMBOLSO", "1.234,56")));

        var result = await _sut.ParseAsync(pdf, null);

        result.Select(r => r.Amount).Should().Equal(-1234.56m, 1234.56m);
    }

    [Fact]
    public async Task ParseAsync_MultiWordDescription_JoinsWordsInOrder()
    {
        using var pdf = BuildPdf(SimplePage("01/03/2025 a 31/03/2025",
            Row(730, "05/03", "05/03", "Compra", "PADARIA DO ZE FILIAL 02 SAO PAULO", "-R$ 12,50")));

        var result = await _sut.ParseAsync(pdf, null);

        result.Should().ContainSingle();
        result[0].Description.Should().Be("PADARIA DO ZE FILIAL 02 SAO PAULO");
    }

    [Fact]
    public async Task ParseAsync_MultipleRows_PreservesTopToBottomOrder()
    {
        using var pdf = BuildPdf(SimplePage("01/03/2025 a 31/03/2025",
            Row(730, "01/03", "01/03", "Pix", "PRIMEIRO", "R$ 1,00"),
            Row(710, "02/03", "02/03", "Pix", "SEGUNDO", "R$ 2,00"),
            Row(690, "03/03", "03/03", "Pix", "TERCEIRO", "R$ 3,00")));

        var result = await _sut.ParseAsync(pdf, null);

        result.Select(r => r.Description).Should().Equal("PRIMEIRO", "SEGUNDO", "TERCEIRO");
    }

    // ── Ignorados ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task ParseAsync_SaldoDoDiaRow_IsIgnored()
    {
        using var pdf = BuildPdf(SimplePage("01/03/2025 a 31/03/2025",
            Row(730, "05/03", "05/03", "Compra", "LOJA A", "-R$ 10,00"),
            new[] { new Cell("Saldo do dia", XEvent, 710), new Cell("R$ 990,00", XAmount, 710) },
            Row(690, "06/03", "06/03", "Compra", "LOJA B", "-R$ 20,00")));

        var result = await _sut.ParseAsync(pdf, null);

        result.Select(r => r.Description).Should().Equal("LOJA A", "LOJA B");
    }

    [Fact]
    public async Task ParseAsync_RepeatedHeaderOnSecondPage_IsIgnoredAndRowsFromBothPagesReturned()
    {
        using var pdf = BuildPdf(
            SimplePage("01/03/2025 a 31/03/2025",
                Row(730, "05/03", "05/03", "Compra", "LOJA A", "-R$ 10,00")),
            SimplePage("01/03/2025 a 31/03/2025",
                Row(730, "20/03", "20/03", "Compra", "LOJA B", "-R$ 20,00")));

        var result = await _sut.ParseAsync(pdf, null);

        result.Should().HaveCount(2);
        result.Select(r => r.Description).Should().Equal("LOJA A", "LOJA B");
    }

    [Fact]
    public async Task ParseAsync_ValidPdfWithoutTableRows_ReturnsEmptyList()
    {
        using var pdf = BuildPdf(SimplePage("01/03/2025 a 31/03/2025"));

        var result = await _sut.ParseAsync(pdf, null);

        result.Should().BeEmpty();
    }

    // ── Coordenadas ───────────────────────────────────────────────────────────

    [Fact]
    public async Task ParseAsync_SlightYDifferenceAndShuffledXOrder_StillGroupsIntoOneRow()
    {
        // Y levemente diferente por palavra (dentro da tolerância) e ordem de emissão embaralhada
        var shuffled = new[]
        {
            new Cell("-R$ 45,00", XAmount, 729.2),
            new Cell("LANCHONETE", XDesc, 730.6),
            new Cell("Compra", XType, 730.0),
            new Cell("07/03", XPosting, 729.6),
            new Cell("07/03", XEvent, 730.4),
            new Cell("CENTRO", XDesc + 70, 729.8)
        };
        using var pdf = BuildPdf(SimplePage("01/03/2025 a 31/03/2025", shuffled));

        var result = await _sut.ParseAsync(pdf, null);

        result.Should().ContainSingle();
        result[0].Description.Should().Be("LANCHONETE CENTRO");
        result[0].Amount.Should().Be(-45.00m);
        result[0].EventDate.Should().Be(new DateOnly(2025, 3, 7));
    }

    [Fact]
    public async Task ParseAsync_ColumnsShiftedRelativeToDefault_UsesHeaderXRanges()
    {
        // Colunas deslocadas: as faixas devem vir do cabeçalho, não de posição fixa
        var header = new[]
        {
            new Cell("Data lançamento", 30, 760), new Cell("Data contábil", 130, 760),
            new Cell("Tipo", 220, 760), new Cell("Descrição", 300, 760), new Cell("Valor", 500, 760)
        };
        var row = new[]
        {
            new Cell("08/03", 30, 730), new Cell("09/03", 130, 730), new Cell("Pix", 220, 730),
            new Cell("JOAO SILVA", 300, 730), new Cell("-R$ 50,00", 500, 730)
        };
        using var pdf = BuildPdf(PeriodHeader("01/03/2025 a 31/03/2025").Concat(header).Concat(row).ToArray());

        var result = await _sut.ParseAsync(pdf, null);

        result.Should().ContainSingle();
        result[0].PostingDate.Should().Be(new DateOnly(2025, 3, 9));
        result[0].RawType.Should().Be("Pix");
        result[0].Description.Should().Be("JOAO SILVA");
        result[0].Amount.Should().Be(-50.00m);
    }

    // ── Ano do período ────────────────────────────────────────────────────────

    [Fact]
    public async Task ParseAsync_PeriodSpanningYearBoundary_ResolvesYearPerMonth()
    {
        using var pdf = BuildPdf(SimplePage("15/12/2024 a 14/01/2025",
            Row(730, "20/12", "20/12", "Compra", "LOJA DEZ", "-R$ 10,00"),
            Row(710, "05/01", "06/01", "Compra", "LOJA JAN", "-R$ 20,00")));

        var result = await _sut.ParseAsync(pdf, null);

        result.Should().HaveCount(2);
        result[0].EventDate.Should().Be(new DateOnly(2024, 12, 20));
        result[0].PostingDate.Should().Be(new DateOnly(2024, 12, 20));
        result[1].EventDate.Should().Be(new DateOnly(2025, 1, 5));
        result[1].PostingDate.Should().Be(new DateOnly(2025, 1, 6));
    }

    [Fact]
    public async Task ParseAsync_EventInDecemberPostedInJanuary_PostingDateIsNextYear()
    {
        using var pdf = BuildPdf(SimplePage("15/12/2024 a 14/01/2025",
            Row(730, "31/12", "02/01", "Compra", "LOJA VIRADA", "-R$ 30,00")));

        var result = await _sut.ParseAsync(pdf, null);

        result.Should().ContainSingle();
        result[0].EventDate.Should().Be(new DateOnly(2024, 12, 31));
        result[0].PostingDate.Should().Be(new DateOnly(2025, 1, 2));
    }

    [Fact]
    public async Task ParseAsync_PeriodWithinSingleYear_UsesThatYear()
    {
        using var pdf = BuildPdf(SimplePage("01/06/2023 a 30/06/2023",
            Row(730, "15/06", "15/06", "Pix", "TESTE", "R$ 1,00")));

        var result = await _sut.ParseAsync(pdf, null);

        result[0].EventDate.Year.Should().Be(2023);
    }

    // ── Senha ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ParseAsync_EncryptedPdfWithCorrectPassword_ReturnsEntries()
    {
        using var plain = BuildPdf(SimplePage("01/03/2025 a 31/03/2025",
            Row(730, "05/03", "05/03", "Compra", "LOJA SEGURA", "-R$ 10,00")));
        using var encrypted = Encrypt(plain, "senha123");

        var result = await _sut.ParseAsync(encrypted, "senha123");

        result.Should().ContainSingle();
        result[0].Description.Should().Be("LOJA SEGURA");
    }

    [Fact]
    public async Task ParseAsync_EncryptedPdfWithWrongPassword_ThrowsDomainException()
    {
        using var plain = BuildPdf(SimplePage("01/03/2025 a 31/03/2025",
            Row(730, "05/03", "05/03", "Compra", "LOJA SEGURA", "-R$ 10,00")));
        using var encrypted = Encrypt(plain, "senha123");

        var act = () => _sut.ParseAsync(encrypted, "errada");

        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task ParseAsync_EncryptedPdfWithoutPassword_ThrowsDomainException()
    {
        using var plain = BuildPdf(SimplePage("01/03/2025 a 31/03/2025",
            Row(730, "05/03", "05/03", "Compra", "LOJA SEGURA", "-R$ 10,00")));
        using var encrypted = Encrypt(plain, "senha123");

        var act = () => _sut.ParseAsync(encrypted, null);

        await act.Should().ThrowAsync<DomainException>();
    }

    // ── Entrada inválida ──────────────────────────────────────────────────────

    [Fact]
    public async Task ParseAsync_StreamThatIsNotPdf_ThrowsDomainException()
    {
        using var notPdf = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("isto nao e um pdf"));

        var act = () => _sut.ParseAsync(notPdf, null);

        await act.Should().ThrowAsync<DomainException>();
    }

    // ── Cultura ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task ParseAsync_UnderInvariantThreadCulture_StillParsesPtBrAmounts()
    {
        var original = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
            using var pdf = BuildPdf(SimplePage("01/03/2025 a 31/03/2025",
                Row(730, "05/03", "05/03", "Boleto", "ALUGUEL", "-R$ 1.234,56")));

            var result = await _sut.ParseAsync(pdf, null);

            result[0].Amount.Should().Be(-1234.56m);
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = original;
        }
    }
}
