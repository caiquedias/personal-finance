using ClosedXML.Excel;
using PersonalFinance.Application.DTOs.Import;
using PersonalFinance.Application.Interfaces;
using PersonalFinance.Domain.Enums;
using System.Text.RegularExpressions;

namespace PersonalFinance.Infrastructure.Services;

/// <summary>
/// Implementação do IExcelParserService usando ClosedXML.
///
/// Regras de parsing da planilha legada:
///
/// TABELA 1 — Despesas Planejadas (cols A–E, a partir da linha 6):
///   A = Fonte (Despesa Parental/Própria ou [MEUS PAIS]/[MEU APÊ] em Ago/2024)
///   B = Descrição com data embutida ex: "Internet (venc. 20/08)"
///   C = Valor decimal
///   D = Quinzena (1.0 = 1ª, 2.0 = 2ª, 0.0 = pago no mês anterior → ignorar)
///   E = Pago? (SIM/NÃO)
///
/// TABELA 2 — Movimentação Bancária (cols P e Q):
///   P = Gastos Diversos — valor + cor da célula = categoria
///   Q = Valores Recebidos — valor + cor da célula
///   Ranges de 1ª/2ª quinzena extraídos das fórmulas em B4/C4
///
/// RECEITA PRIMÁRIA — valor hardcoded na fórmula SUM em B4/C4:
///   =SUM(16812.91)+sum(Q7:Q300)-sum(P7:P300)
///   O número dentro do SUM() é a receita da quinzena.
/// </summary>
public sealed class ExcelParserService : IExcelParserService
{
    // ── Mapa cor (ARGB hex) → nome da categoria ───────────────────────────────
    private static readonly Dictionary<string, string> ColorCategoryMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["FF00FFFF"] = "Gastos com mercado meus pais",
            ["FF0000FF"] = "Gastos com mercado meu Apê",
            ["FFFFFF00"] = "Gasto fútil",
            ["FFFF9900"] = "Serviços",
            ["FF980000"] = "Depósitos de PF (declarar)",
            ["FFFF00FF"] = "Investimento + Rendimento",
            ["FF93C47D"] = "Despesa Gatinhos",
            ["FFB4A7D6"] = "Gasolina",
            ["FF000000"] = "Uber",
        };

    // ── Nomes dos meses em PT-BR → número ─────────────────────────────────────
    private static readonly Dictionary<string, int> MonthNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Janeiro"] = 1,
            ["Fevereiro"] = 2,
            ["Março"] = 3,
            ["Abril"] = 4,
            ["Maio"] = 5,
            ["Junho"] = 6,
            ["Julho"] = 7,
            ["Agosto"] = 8,
            ["Setembro"] = 9,
            ["Outubro"] = 10,
            ["Novembro"] = 11,
            ["Dezembro"] = 12,
        };

    // Regex para extrair data da descrição ex: "Internet (venc. 20/08)"
    private static readonly Regex DateRegex = new(
        @"\(\s*(?:venc\.?\s*)?(\d{1,2})[/\-](\d{1,2})\s*\)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Regex para extrair range de linhas da fórmula ex: "sum(P7:P65)"
    private static readonly Regex RangeRegex = new(
        @"sum\(P(\d+):P(\d+)\)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Regex para extrair receita primária ex: "=SUM(6899.19)+..."
    private static readonly Regex PrimaryIncomeRegex = new(
        @"=\s*SUM\(\s*([\d.,]+)\s*\)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Regex para receita da 2ª quinzena ex: "=5500+sum(...)"
    private static readonly Regex Income2Regex = new(
        @"^=\s*([\d.]+)\s*\+",
        RegexOptions.Compiled);

    public Task<IReadOnlyList<ParsedSheetDto>> ParseAsync(
        Stream fileStream,
        CancellationToken ct = default)
    {
        using var workbook = new XLWorkbook(fileStream);
        var result = new List<ParsedSheetDto>();

        foreach (var sheet in workbook.Worksheets)
        {
            var parsed = ParseSheet(sheet);
            if (parsed is not null)
                result.Add(parsed);
        }

        return Task.FromResult<IReadOnlyList<ParsedSheetDto>>(result);
    }

    // ── Parser de aba ─────────────────────────────────────────────────────────

    private ParsedSheetDto? ParseSheet(IXLWorksheet sheet)
    {
        var warnings = new List<string>();

        if (!TryParseSheetName(sheet.Name, out int year, out int month))
        {
            warnings.Add($"Aba '{sheet.Name}' não reconhecida como período — ignorada.");
            return null;
        }

        // Lê ranges de quinzena das fórmulas B4/C4
        var b4Cell = sheet.Cell("B4");
        var c4Cell = sheet.Cell("C4");
        var b4Formula = b4Cell.FormulaA1 ?? string.Empty;
        var c4Formula = c4Cell.FormulaA1 ?? string.Empty;

        var (q1Start, q1End) = ExtractPRange(b4Formula, fallbackStart: 7, fallbackEnd: 500);
        var (q2Start, q2End) = ExtractPRange(c4Formula, fallbackStart: 0, fallbackEnd: 0);

        // Receita primária — tenta fórmula, cai no valor em cache se não houver fórmula
        var income1 = ExtractPrimaryIncome(b4Cell);
        var income2 = ExtractSecondaryIncome(c4Cell);

        var incomes = new List<ParsedIncomeDto>();
        var expenses = new List<ParsedExpenseDto>();

        // ── Receitas primárias ─────────────────────────────────────────────
        if (income1 > 0)
        {
            incomes.Add(new ParsedIncomeDto(
                Description: "Receita 1ª Quinzena",
                Amount: income1,
                FortnightType: FortnightType.First,
                ReceivedAt: new DateOnly(year, month, 1)));
        }

        if (income2 > 0)
        {
            incomes.Add(new ParsedIncomeDto(
                Description: "Receita 2ª Quinzena",
                Amount: income2,
                FortnightType: FortnightType.Second,
                ReceivedAt: new DateOnly(year, month, 15)));
        }

        // ── Tabela 1: Despesas Planejadas ──────────────────────────────────
        ParsePlannedExpenses(sheet, year, month, expenses, warnings);

        // ── Tabela 2: Gastos Diversos (col P) ─────────────────────────────
        if (q1Start > 0)
            ParseDiverseExpenses(sheet, q1Start, q1End, FortnightType.First,
                year, month, expenses, warnings);

        if (q2Start > 0)
            ParseDiverseExpenses(sheet, q2Start, q2End, FortnightType.Second,
                year, month, expenses, warnings);

        // ── Tabela 2: Valores Recebidos (col Q) ───────────────────────────
        if (q1Start > 0)
            ParseDiverseIncomes(sheet, q1Start, q1End, FortnightType.First,
                year, month, incomes, warnings);

        if (q2Start > 0)
            ParseDiverseIncomes(sheet, q2Start, q2End, FortnightType.Second,
                year, month, incomes, warnings);

        return new ParsedSheetDto(year, month, incomes, expenses, warnings);
    }

    // ── Despesas planejadas (tabela A-E) ─────────────────────────────────────

    private static void ParsePlannedExpenses(
        IXLWorksheet sheet,
        int year,
        int month,
        List<ParsedExpenseDto> expenses,
        List<string> warnings)
    {
        // Linha de cabeçalho é identificada pelo conteúdo — não por número fixo
        int dataStartRow = FindPlannedExpenseHeaderRow(sheet) + 1;
        if (dataStartRow <= 1)
        {
            warnings.Add($"Cabeçalho da tabela de despesas não encontrado em {month:D2}/{year}.");
            return;
        }

        for (int row = dataStartRow; row <= dataStartRow + 100; row++)
        {
            var colA = sheet.Cell(row, 1).GetString().Trim();
            var colB = sheet.Cell(row, 2).GetString().Trim();
            var colC = sheet.Cell(row, 3);
            var colD = sheet.Cell(row, 4);
            var colE = sheet.Cell(row, 5).GetString().Trim();

            // Linha em branco ou sem descrição — fim da tabela
            if (string.IsNullOrWhiteSpace(colB)) break;

            // Valor não numérico — pula (linha de sumário/título)
            if (!colC.TryGetValue(out double amount) || amount <= 0) continue;

            // Quinzena 0 = pago no mês anterior — ignorar conforme decisão
            if (!colD.TryGetValue(out double quinzenaRaw) || quinzenaRaw == 0.0) continue;

            var fortnight = quinzenaRaw >= 2.0
                ? FortnightType.Second
                : FortnightType.First;

            var sourceType = NormalizeSourceType(colA);
            var isPaid = colE.Equals("SIM", StringComparison.OrdinalIgnoreCase);
            var dueDate = ExtractDueDate(colB, year, month, fortnight);

            expenses.Add(new ParsedExpenseDto(
                Description: colB,
                Amount: (decimal)amount,
                SourceType: sourceType,
                FortnightType: fortnight,
                IsPaid: isPaid,
                DueDate: dueDate,
                CategoryName: "Gasto Recorrente"
            ));
        }
    }

    // ── Gastos Diversos (col P, por cor) ─────────────────────────────────────

    private static void ParseDiverseExpenses(
        IXLWorksheet sheet,
        int rowStart,
        int rowEnd,
        FortnightType fortnight,
        int year,
        int month,
        List<ParsedExpenseDto> expenses,
        List<string> warnings)
    {
        var dueDate = fortnight == FortnightType.First
            ? new DateOnly(year, month, 1)
            : new DateOnly(year, month, 15);

        for (int row = rowStart; row <= rowEnd; row++)
        {
            var cell = sheet.Cell(row, 16); // col P
            if (!cell.TryGetValue(out double amount) || amount <= 0) continue;

            var categoryName = GetCategoryFromColor(cell);
            if (categoryName is null)
            {
                // Sem cor reconhecida e sem valor de categoria → pula silenciosamente
                continue;
            }

            // Ignorar linhas de total (TOTAL GASTOS, TOTAL EM INVESTIMENTOS)
            if (categoryName.StartsWith("TOTAL", StringComparison.OrdinalIgnoreCase))
                continue;

            expenses.Add(new ParsedExpenseDto(
                Description: categoryName,
                Amount: (decimal)amount,
                SourceType: SourceType.Personal,
                FortnightType: fortnight,
                IsPaid: true,
                DueDate: dueDate,
                CategoryName: categoryName
            ));
        }
    }

    // ── Valores Recebidos (col Q, por cor) ───────────────────────────────────

    private static void ParseDiverseIncomes(
        IXLWorksheet sheet,
        int rowStart,
        int rowEnd,
        FortnightType fortnight,
        int year,
        int month,
        List<ParsedIncomeDto> incomes,
        List<string> warnings)
    {
        var receivedAt = fortnight == FortnightType.First
            ? new DateOnly(year, month, 1)
            : new DateOnly(year, month, 15);

        for (int row = rowStart; row <= rowEnd; row++)
        {
            var cell = sheet.Cell(row, 17); // col Q
            if (!cell.TryGetValue(out double amount) || amount <= 0) continue;

            // Valores recebidos: tenta mapear por cor, senão usa "Receita Diversa"
            var colorName = GetCategoryFromColor(cell) ?? "Receita Diversa";

            // Ignora totais
            if (colorName.StartsWith("TOTAL", StringComparison.OrdinalIgnoreCase))
                continue;

            incomes.Add(new ParsedIncomeDto(
                Description: colorName,
                Amount: (decimal)amount,
                FortnightType: fortnight,
                ReceivedAt: receivedAt
            ));
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static bool TryParseSheetName(string name, out int year, out int month)
    {
        year = 0; month = 0;

        // Ex: "Agosto2024", "Março2026"
        var yearMatch = Regex.Match(name, @"(\d{4})$");
        if (!yearMatch.Success) return false;

        year = int.Parse(yearMatch.Groups[1].Value);
        var monthStr = name[..^4];

        if (!MonthNames.TryGetValue(monthStr, out month)) return false;
        return true;
    }

    private static (int start, int end) ExtractPRange(
        string formula, int fallbackStart, int fallbackEnd)
    {
        var m = RangeRegex.Match(formula);
        if (!m.Success) return (fallbackStart, fallbackEnd);
        return (int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value));
    }

    private static decimal ExtractPrimaryIncome(IXLCell b4Cell)
    {
        var formula = b4Cell.FormulaA1 ?? string.Empty;

        // Caminho 1 — fórmula: =SUM(2613.15)+sum(Q7:Q500)-sum(P7:P500)
        if (!string.IsNullOrWhiteSpace(formula))
        {
            var m = PrimaryIncomeRegex.Match(formula);
            if (m.Success)
            {
                var raw = m.Groups[1].Value.Replace(',', '.');
                if (decimal.TryParse(raw, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var val))
                    return val;
            }
        }

        // Caminho 2 — planilha salva sem fórmula, lê o valor calculado em cache
        if (b4Cell.TryGetValue(out double cached) && cached > 0)
            return (decimal)cached;

        return 0;
    }

    private static decimal ExtractSecondaryIncome(IXLCell c4Cell)
    {
        var formula = c4Cell.FormulaA1 ?? string.Empty;

        // Caminho 1 — fórmula: =5500+sum(Q51:Q100)-sum(P51:P100)
        if (!string.IsNullOrWhiteSpace(formula))
        {
            var m = Income2Regex.Match(formula.TrimStart('='));
            if (m.Success &&
                decimal.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var val))
                return val;
        }

        // Caminho 2 — valor calculado em cache
        if (c4Cell.TryGetValue(out double cached) && cached > 0)
            return (decimal)cached;

        return 0;
    }

    private static int FindPlannedExpenseHeaderRow(IXLWorksheet sheet)
    {
        for (int row = 1; row <= 20; row++)
        {
            var a = sheet.Cell(row, 1).GetString();
            var b = sheet.Cell(row, 2).GetString();
            if ((a == "Fonte") &&
                (b is "Dívida" or "Dívidas")) return row;
        }
        return -1;
    }

    private static SourceType NormalizeSourceType(string? raw) =>
        raw?.Trim() switch
        {
            "Despesa Parental" or "[MEUS PAIS]" => SourceType.Parental,
            "Despesa Própria" or "[MEU APÊ]" => SourceType.Personal,
            _ => SourceType.Personal,
        };

    /// <summary>
    /// Extrai a data de vencimento da descrição quando possível.
    /// Fallback: dia 01 para 1ª quinzena, dia 15 para 2ª quinzena.
    /// </summary>
    private static DateOnly ExtractDueDate(
        string description, int year, int month, FortnightType fortnight)
    {
        var m = DateRegex.Match(description);
        if (m.Success)
        {
            if (int.TryParse(m.Groups[1].Value, out int day) &&
                int.TryParse(m.Groups[2].Value, out int mon))
            {
                try { return new DateOnly(year, mon, day); }
                catch { /* dia inválido — usa fallback */ }
            }
        }

        return fortnight == FortnightType.First
            ? new DateOnly(year, month, 1)
            : new DateOnly(year, month, 15);
    }

    /// <summary>
    /// Retorna o nome da categoria baseado na cor de fundo da célula.
    /// Retorna null se a cor não estiver no mapa (sem cor / branco / transparente).
    /// </summary>
    private static string? GetCategoryFromColor(IXLCell cell)
    {
        try
        {
            var fill = cell.Style.Fill;
            if (fill is null) return null;

            // ClosedXML expõe ARGB como inteiro
            var color = fill.BackgroundColor;
            if (color == null || color.Color.IsEmpty) return null;

            if (color.ColorType != XLColorType.Color) return null;

            var argb = color.Color; // System.Drawing.Color
            var hex = $"{argb.A:X2}{argb.R:X2}{argb.G:X2}{argb.B:X2}";

            // Transparente ou branco → sem categoria
            if (hex is "00000000" or "FFFFFFFF") return null;

            return ColorCategoryMap.TryGetValue(hex, out var name) ? name : null;
        }
        catch
        {
            return null;
        }
    }
}
