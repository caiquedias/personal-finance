using ClosedXML.Excel;
using FluentAssertions;
using PersonalFinance.Domain.Enums;
using PersonalFinance.Infrastructure.Services;
using Xunit;

namespace PersonalFinance.Infrastructure.Tests.Unit.Services;

/// <summary>
/// Testes unitários do ExcelParserService.
/// Criam workbooks em memória simulando o formato da planilha legada.
/// </summary>
public class ExcelParserServiceTests
{
    private readonly ExcelParserService _sut = new();

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Cria um workbook mínimo com a estrutura da planilha legada.
    /// </summary>
    private static Stream BuildWorkbook(Action<IXLWorkbook> configure)
    {
        var wb = new XLWorkbook();
        configure(wb);
        var ms = new MemoryStream();
        wb.SaveAs(ms);
        ms.Position = 0;
        return ms;
    }

    private static IXLWorksheet AddSheet(
        IXLWorkbook wb, string name,
        decimal income1, decimal income2,
        int p1Start, int p1End,
        int p2Start = 0, int p2End = 0)
    {
        var ws = wb.AddWorksheet(name);

        // Fórmulas B4 e C4
        ws.Cell("B4").FormulaA1 =
            $"=SUM({income1})+sum(Q{p1Start}:Q{p1End})-sum(P{p1Start}:P{p1End})";

        if (p2Start > 0)
            ws.Cell("C4").FormulaA1 =
                $"={income2}+sum(Q{p2Start}:Q{p2End})-sum(P{p2Start}:P{p2End})";

        // Cabeçalho da tabela de despesas
        ws.Cell(5, 1).Value = "Fonte";
        ws.Cell(5, 2).Value = "Dívida";
        ws.Cell(5, 3).Value = "Valor";
        ws.Cell(5, 4).Value = "Quinzena";
        ws.Cell(5, 5).Value = "Pago?";

        return ws;
    }

    private static void AddPlannedExpense(
        IXLWorksheet ws, int row,
        string fonte, string descricao, decimal valor,
        double quinzena, string pago)
    {
        ws.Cell(row, 1).Value = fonte;
        ws.Cell(row, 2).Value = descricao;
        ws.Cell(row, 3).Value = (double)valor;
        ws.Cell(row, 4).Value = quinzena;
        ws.Cell(row, 5).Value = pago;
    }

    private static void AddGastoDiverso(
        IXLWorksheet ws, int row, decimal valor, string argbColor)
    {
        var cell = ws.Cell(row, 16);
        cell.Value = (double)valor;
        if (!string.IsNullOrEmpty(argbColor))
        {
            // Converte ARGB string para System.Drawing.Color
            var argb = Convert.ToInt32(argbColor, 16);
            cell.Style.Fill.BackgroundColor =
                XLColor.FromArgb(argb >> 16 & 0xFF, argb >> 8 & 0xFF, argb & 0xFF);
        }
    }

    // ── Testes de parsing de nome de aba ──────────────────────────────────────

    [Fact(DisplayName = "Deve parsear nome de aba válido em mês e ano")]
    public async Task Parse_ValidSheetName_ShouldExtractMonthAndYear()
    {
        using var stream = BuildWorkbook(wb =>
            AddSheet(wb, "Abril2026", 2613.15m, 5500m, 7, 50, 51, 100));

        var result = await _sut.ParseAsync(stream);

        result.Should().HaveCount(1);
        result[0].Month.Should().Be(4);
        result[0].Year.Should().Be(2026);
    }

    [Fact(DisplayName = "Deve ignorar aba com nome não reconhecido como período")]
    public async Task Parse_InvalidSheetName_ShouldReturnEmpty()
    {
        using var stream = BuildWorkbook(wb =>
        {
            var ws = wb.AddWorksheet("Resumo Anual");
            ws.Cell("A1").Value = "Dados";
        });

        var result = await _sut.ParseAsync(stream);
        result.Should().BeEmpty();
    }

    [Theory(DisplayName = "Deve parsear todos os meses em PT-BR")]
    [InlineData("Janeiro2025",  1, 2025)]
    [InlineData("Fevereiro2025", 2, 2025)]
    [InlineData("Março2025",    3, 2025)]
    [InlineData("Agosto2024",   8, 2024)]
    [InlineData("Dezembro2025", 12, 2025)]
    public async Task Parse_AllPortugueseMonths_ShouldParsCorrectly(
        string sheetName, int expectedMonth, int expectedYear)
    {
        using var stream = BuildWorkbook(wb =>
            AddSheet(wb, sheetName, 1000m, 0m, 7, 50));

        var result = await _sut.ParseAsync(stream);

        result.Should().HaveCount(1);
        result[0].Month.Should().Be(expectedMonth);
        result[0].Year.Should().Be(expectedYear);
    }

    // ── Testes de receita primária ─────────────────────────────────────────────

    [Fact(DisplayName = "Deve extrair receita da 1ª quinzena do SUM em B4")]
    public async Task Parse_PrimaryIncome_ShouldExtractFromB4Formula()
    {
        using var stream = BuildWorkbook(wb =>
            AddSheet(wb, "Janeiro2026", 9739.80m, 0m, 7, 500));

        var result = await _sut.ParseAsync(stream);

        var incomes = result[0].Incomes;
        incomes.Should().Contain(i =>
            i.Description == "Receita 1ª Quinzena" &&
            i.Amount == 9739.80m &&
            i.FortnightType == FortnightType.First);
    }

    [Fact(DisplayName = "Deve extrair receita da 2ª quinzena do valor inicial em C4")]
    public async Task Parse_SecondaryIncome_ShouldExtractFromC4Formula()
    {
        using var stream = BuildWorkbook(wb =>
            AddSheet(wb, "Março2026", 6147.22m, 5500m, 7, 500, 501, 600));

        var result = await _sut.ParseAsync(stream);

        var incomes = result[0].Incomes;
        incomes.Should().Contain(i =>
            i.Description == "Receita 2ª Quinzena" &&
            i.Amount == 5500m &&
            i.FortnightType == FortnightType.Second);
    }

    [Fact(DisplayName = "Não deve criar receita quando valor é zero")]
    public async Task Parse_ZeroIncome_ShouldNotCreateIncomeRecord()
    {
        using var stream = BuildWorkbook(wb =>
            AddSheet(wb, "Setembro2024", 16636.71m, 0m, 7, 100));

        var result = await _sut.ParseAsync(stream);

        result[0].Incomes.Should().NotContain(i =>
            i.Description == "Receita 2ª Quinzena");
    }

    // ── Testes de despesas planejadas ─────────────────────────────────────────

    [Fact(DisplayName = "Deve parsear despesa planejada com quinzena 1 e fonte Parental")]
    public async Task Parse_PlannedExpense_ShouldMapCorrectly()
    {
        using var stream = BuildWorkbook(wb =>
        {
            var ws = AddSheet(wb, "Abril2026", 2613.15m, 5500m, 7, 50);
            AddPlannedExpense(ws, 6, "Despesa Parental",
                "Taxa de Condominio (10/04)", 600m, 1.0, "NÃO");
        });

        var result = await _sut.ParseAsync(stream);

        var exp = result[0].Expenses.Should().ContainSingle(e =>
            e.Description == "Taxa de Condominio (10/04)").Subject;

        exp.Amount.Should().Be(600m);
        exp.SourceType.Should().Be(SourceType.Parental);
        exp.FortnightType.Should().Be(FortnightType.First);
        exp.IsPaid.Should().BeFalse();
        exp.CategoryName.Should().Be("Gasto Recorrente");
    }

    [Fact(DisplayName = "Deve marcar despesa como paga quando Pago=SIM")]
    public async Task Parse_PlannedExpense_PaidStatus_ShouldBeTrue()
    {
        using var stream = BuildWorkbook(wb =>
        {
            var ws = AddSheet(wb, "Março2026", 6147.22m, 5500m, 7, 50);
            AddPlannedExpense(ws, 6, "Despesa Própria",
                "Plano de Saúde (28/2)", 424.49m, 1.0, "SIM");
        });

        var result = await _sut.ParseAsync(stream);

        result[0].Expenses.Should().ContainSingle(e =>
            e.IsPaid && e.Amount == 424.49m);
    }

    [Fact(DisplayName = "Deve ignorar despesas com quinzena 0")]
    public async Task Parse_PlannedExpense_Quinzena0_ShouldBeSkipped()
    {
        using var stream = BuildWorkbook(wb =>
        {
            var ws = AddSheet(wb, "Janeiro2025", 5342.6m, 0m, 7, 100);
            AddPlannedExpense(ws, 6, "Despesa Própria",
                "Pago no mês anterior", 150m, 0.0, "SIM");
            AddPlannedExpense(ws, 7, "Despesa Própria",
                "Despesa válida", 300m, 1.0, "NÃO");
        });

        var result = await _sut.ParseAsync(stream);

        result[0].Expenses.Should().HaveCount(1);
        result[0].Expenses[0].Description.Should().Be("Despesa válida");
    }

    [Fact(DisplayName = "Deve normalizar fonte [MEUS PAIS] para SourceType.Parental")]
    public async Task Parse_NormalizeAgosto2024Sources_ShouldMapCorrectly()
    {
        using var stream = BuildWorkbook(wb =>
        {
            var ws = AddSheet(wb, "Agosto2024", 16812.91m, 0m, 7, 300);
            AddPlannedExpense(ws, 6, "[MEUS PAIS]",
                "Internet (venc. 20/08)", 105.16m, 1.0, "SIM");
            AddPlannedExpense(ws, 7, "[MEU APÊ]",
                "Meu aluguel (20/08)", 1678.62m, 1.0, "SIM");
        });

        var result = await _sut.ParseAsync(stream);

        result[0].Expenses.Should().Contain(e => e.SourceType == SourceType.Parental);
        result[0].Expenses.Should().Contain(e => e.SourceType == SourceType.Personal);
    }

    // ── Testes de extração de data ────────────────────────────────────────────

    [Theory(DisplayName = "Deve extrair data de vencimento do texto da descrição")]
    [InlineData("Internet (venc. 20/08)", 2026, 4, 1, 2026, 8, 20)]
    [InlineData("Taxa de Condominio (10/04)", 2026, 4, 1, 2026, 4, 10)]
    [InlineData("Plano de Saúde (28/2)", 2026, 3, 1, 2026, 2, 28)]
    public async Task Parse_DueDate_ShouldExtractFromDescription(
        string descricao, int year, int month, double quinzena,
        int expectedYear, int expectedMonth, int expectedDay)
    {
        using var stream = BuildWorkbook(wb =>
        {
            var ws = AddSheet(wb, $"{MonthNameFor(month)}{year}", 1000m, 0m, 7, 50);
            AddPlannedExpense(ws, 6, "Despesa Própria", descricao, 100m, quinzena, "NÃO");
        });

        var result = await _sut.ParseAsync(stream);

        result[0].Expenses[0].DueDate.Should()
            .Be(new DateOnly(expectedYear, expectedMonth, expectedDay));
    }

    [Fact(DisplayName = "Deve usar dia 01 como fallback para 1ª quinzena sem data no texto")]
    public async Task Parse_DueDate_NoDateInText_FirstFortnight_ShouldUseDayOne()
    {
        using var stream = BuildWorkbook(wb =>
        {
            var ws = AddSheet(wb, "Abril2026", 2613.15m, 5500m, 7, 50);
            AddPlannedExpense(ws, 6, "Despesa Própria",
                "Despesa sem data", 100m, 1.0, "NÃO");
        });

        var result = await _sut.ParseAsync(stream);

        result[0].Expenses[0].DueDate.Should().Be(new DateOnly(2026, 4, 1));
    }

    [Fact(DisplayName = "Deve usar dia 15 como fallback para 2ª quinzena sem data no texto")]
    public async Task Parse_DueDate_NoDateInText_SecondFortnight_ShouldUseDayFifteen()
    {
        using var stream = BuildWorkbook(wb =>
        {
            var ws = AddSheet(wb, "Março2026", 6147.22m, 5500m, 7, 50, 51, 100);
            AddPlannedExpense(ws, 6, "Despesa Própria",
                "Despesa sem data", 100m, 2.0, "NÃO");
        });

        var result = await _sut.ParseAsync(stream);

        result[0].Expenses[0].DueDate.Should().Be(new DateOnly(2026, 3, 15));
    }

    // ── Testes de gastos diversos por cor ─────────────────────────────────────

    [Fact(DisplayName = "Deve mapear cor amarela para categoria Gasto fútil")]
    public async Task Parse_YellowCell_ShouldMapToGastoFutil()
    {
        using var stream = BuildWorkbook(wb =>
        {
            var ws = AddSheet(wb, "Julho2025", 6899.19m, 4200m, 7, 65, 66, 202);
            AddGastoDiverso(ws, 10, 122.09m, "FFFFFF00"); // Amarelo
        });

        var result = await _sut.ParseAsync(stream);

        result[0].Expenses.Should().Contain(e =>
            e.CategoryName == "Gasto fútil" &&
            e.Amount == 122.09m &&
            e.FortnightType == FortnightType.First);
    }

    [Fact(DisplayName = "Deve mapear cor preta para categoria Uber")]
    public async Task Parse_BlackCell_ShouldMapToUber()
    {
        using var stream = BuildWorkbook(wb =>
        {
            var ws = AddSheet(wb, "Outubro2025", 12000m, 0m, 7, 500);
            AddGastoDiverso(ws, 8, 69.26m, "FF000000"); // Preto
        });

        var result = await _sut.ParseAsync(stream);

        result[0].Expenses.Should().Contain(e =>
            e.CategoryName == "Uber" &&
            e.Amount == 69.26m);
    }

    [Fact(DisplayName = "Deve ignorar células sem cor reconhecida nos gastos diversos")]
    public async Task Parse_UncoloredCell_ShouldBeIgnored()
    {
        using var stream = BuildWorkbook(wb =>
        {
            var ws = AddSheet(wb, "Abril2026", 2613.15m, 5500m, 7, 50);
            // Célula P sem cor específica
            ws.Cell(10, 16).Value = 50.0;
        });

        var result = await _sut.ParseAsync(stream);

        // Nenhum gasto diverso deve ter sido importado
        result[0].Expenses
            .Where(e => e.CategoryName != "Gasto Recorrente")
            .Should().BeEmpty();
    }

    [Fact(DisplayName = "Gastos diversos da 1ª quinzena devem ter FortnightType.First")]
    public async Task Parse_DiverseExpense_FirstFortnightRange_ShouldBeFirstFortnight()
    {
        using var stream = BuildWorkbook(wb =>
        {
            var ws = AddSheet(wb, "Julho2025", 6899.19m, 4200m, 7, 65, 66, 202);
            AddGastoDiverso(ws, 20, 36.8m, "FFFFFF00"); // linha 20 = 1ª quinzena (7-65)
        });

        var result = await _sut.ParseAsync(stream);

        result[0].Expenses.Should().Contain(e =>
            e.Amount == 36.8m &&
            e.FortnightType == FortnightType.First);
    }

    [Fact(DisplayName = "Gastos diversos da 2ª quinzena devem ter FortnightType.Second")]
    public async Task Parse_DiverseExpense_SecondFortnightRange_ShouldBeSecondFortnight()
    {
        using var stream = BuildWorkbook(wb =>
        {
            var ws = AddSheet(wb, "Julho2025", 6899.19m, 4200m, 7, 65, 66, 202);
            AddGastoDiverso(ws, 70, 5.29m, "FF0000FF"); // linha 70 = 2ª quinzena (66-202)
        });

        var result = await _sut.ParseAsync(stream);

        result[0].Expenses.Should().Contain(e =>
            e.Amount == 5.29m &&
            e.FortnightType == FortnightType.Second);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string MonthNameFor(int month) => month switch
    {
        1 => "Janeiro", 2 => "Fevereiro", 3 => "Março",    4 => "Abril",
        5 => "Maio",    6 => "Junho",     7 => "Julho",    8 => "Agosto",
        9 => "Setembro",10 => "Outubro",  11 => "Novembro",12 => "Dezembro",
        _ => "Janeiro"
    };
}
