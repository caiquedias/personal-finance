using System.Globalization;
using System.Text.RegularExpressions;
using PersonalFinance.Application.DTOs.Import;
using PersonalFinance.Application.Interfaces;
using PersonalFinance.Domain.Exceptions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Exceptions;

namespace PersonalFinance.Infrastructure.Services;

/// <summary>
/// Parser do extrato em PDF do C6 Bank. Reconstrói as linhas da tabela pela posição
/// (x/y) de cada palavra, pois a extração de texto simples embaralha as colunas.
/// </summary>
public sealed class C6StatementPdfParserService : IStatementParserService
{
    private static readonly CultureInfo PtBr = new("pt-BR");
    private static readonly Regex PeriodRegex =
        new(@"(\d{2}/\d{2}/\d{4})\s*a\s*(\d{2}/\d{2}/\d{4})", RegexOptions.Compiled);
    private static readonly Regex DayMonthRegex =
        new(@"^(\d{2})/(\d{2})$", RegexOptions.Compiled);

    // Tolerância (em pontos) para agrupar palavras na mesma linha e para bordas de coluna
    private const double YTolerance = 3.0;
    private const double XTolerance = 2.0;

    private sealed record Columns(double Event, double Posting, double Type, double Description, double Amount);

    public Task<IReadOnlyList<ParsedStatementEntryDto>> ParseAsync(
        Stream pdfStream, string? password, CancellationToken ct = default)
    {
        var entries = new List<ParsedStatementEntryDto>();

        using var document = OpenDocument(pdfStream, password);

        try
        {
            ReadPages(document, entries, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not DomainException)
        {
            // GetPages()/GetWords() são lazy: falhas de leitura só aparecem aqui
            throw new DomainException("Não foi possível ler o conteúdo do PDF.", ex);
        }

        return Task.FromResult<IReadOnlyList<ParsedStatementEntryDto>>(entries);
    }

    private static void ReadPages(PdfDocument document, List<ParsedStatementEntryDto> entries, CancellationToken ct)
    {
        DateOnly? periodStart = null, periodEnd = null;
        Columns? columns = null;

        foreach (var page in document.GetPages())
        {
            ct.ThrowIfCancellationRequested();

            var lines = GroupIntoLines(page.GetWords());

            if (periodStart is null)
            {
                foreach (var line in lines)
                {
                    var m = PeriodRegex.Match(string.Join(" ", line.Select(w => w.Text)));
                    if (!m.Success) continue;
                    if (TryParseFullDate(m.Groups[1].Value, out var s) && TryParseFullDate(m.Groups[2].Value, out var e))
                    {
                        periodStart = s;
                        periodEnd = e;
                        break;
                    }
                }
            }

            // Cabeçalho da página define as faixas de X; se ausente, reaproveita o anterior
            var headerIndex = lines.FindIndex(l => TryParseHeader(l, out _));
            if (headerIndex >= 0)
                TryParseHeader(lines[headerIndex], out columns);

            if (columns is null || periodStart is null || periodEnd is null)
                continue;

            for (var i = headerIndex + 1; i < lines.Count; i++)
            {
                var entry = ParseRow(lines[i], columns, periodStart.Value, periodEnd.Value, page.Number, i + 1);
                if (entry is not null) entries.Add(entry);
            }
        }
    }

    private static PdfDocument OpenDocument(Stream stream, string? password)
    {
        try
        {
            // PdfPig exige stream seekable
            var source = stream;
            if (!stream.CanSeek)
            {
                var copy = new MemoryStream();
                stream.CopyTo(copy);
                copy.Position = 0;
                source = copy;
            }

            var options = new ParsingOptions { UseLenientParsing = true };
            if (password is not null)
                options.Password = password;
            return PdfDocument.Open(source, options);
        }
        catch (PdfDocumentEncryptedException ex)
        {
            throw new DomainException("Senha do extrato ausente ou incorreta.", ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new DomainException("O arquivo enviado não é um PDF válido.", ex);
        }
    }

    /// <summary>Agrupa palavras por Y (tolerância), de cima para baixo, e ordena por X.</summary>
    private static List<List<Word>> GroupIntoLines(IEnumerable<Word> words)
    {
        var lines = new List<(double Y, List<Word> Words)>();
        foreach (var word in words.OrderByDescending(w => w.BoundingBox.Bottom))
        {
            var y = word.BoundingBox.Bottom;
            var line = lines.FirstOrDefault(l => Math.Abs(l.Y - y) <= YTolerance);
            if (line.Words is null)
                lines.Add((y, new List<Word> { word }));
            else
                line.Words.Add(word);
        }

        return lines
            .OrderByDescending(l => l.Y)
            .Select(l => l.Words.OrderBy(w => w.BoundingBox.Left).ToList())
            .ToList();
    }

    private static bool TryParseHeader(List<Word> line, out Columns? columns)
    {
        columns = null;
        var datas = line.Where(w => w.Text == "Data").ToList();
        var tipo = line.FirstOrDefault(w => w.Text == "Tipo");
        var desc = line.FirstOrDefault(w => w.Text.StartsWith("Descri", StringComparison.Ordinal));
        var valor = line.FirstOrDefault(w => w.Text == "Valor");
        if (datas.Count < 2 || tipo is null || desc is null || valor is null)
            return false;

        columns = new Columns(
            datas[0].BoundingBox.Left, datas[1].BoundingBox.Left,
            tipo.BoundingBox.Left, desc.BoundingBox.Left, valor.BoundingBox.Left);
        return true;
    }

    private static ParsedStatementEntryDto? ParseRow(
        List<Word> line, Columns c, DateOnly periodStart, DateOnly periodEnd, int pageNumber, int lineNumber)
    {
        var cells = new[] { new List<string>(), new List<string>(), new List<string>(), new List<string>(), new List<string>() };
        foreach (var w in line)
        {
            var x = w.BoundingBox.Left + XTolerance;
            var idx = x >= c.Amount ? 4 : x >= c.Description ? 3 : x >= c.Type ? 2 : x >= c.Posting ? 1 : 0;
            cells[idx].Add(w.Text);
        }

        var eventMatch = DayMonthRegex.Match(string.Join(" ", cells[0]));
        var postingMatch = DayMonthRegex.Match(string.Join(" ", cells[1]));
        if (!eventMatch.Success || !postingMatch.Success)
            return null;

        if (!TryResolveDate(eventMatch, periodStart, periodEnd, out var eventDate) ||
            !TryResolveDate(postingMatch, periodStart, periodEnd, out var postingDate) ||
            !TryParseAmount(string.Join(" ", cells[4]), out var amount))
            // Sem descrição/valor bruto na mensagem: dados financeiros sensíveis
            throw new DomainException(
                $"Linha de lançamento inválida no extrato (página {pageNumber}, linha {lineNumber}).");

        return new ParsedStatementEntryDto(
            eventDate, postingDate,
            string.Join(" ", cells[2]), string.Join(" ", cells[3]), amount);
    }

    /// <summary>Resolve o ano de "dd/MM" a partir do período, considerando virada dez→jan.</summary>
    private static bool TryResolveDate(Match m, DateOnly start, DateOnly end, out DateOnly date)
    {
        date = default;
        var day = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        var month = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
        var year = start.Year == end.Year || month >= start.Month ? start.Year : end.Year;

        if (month is < 1 or > 12 || day < 1 || day > DateTime.DaysInMonth(year, month))
            return false;

        date = new DateOnly(year, month, day);
        return true;
    }

    private static bool TryParseFullDate(string text, out DateOnly date) =>
        DateOnly.TryParseExact(text, "dd/MM/yyyy", PtBr, DateTimeStyles.None, out date);

    private static bool TryParseAmount(string text, out decimal amount)
    {
        var clean = text.Replace("R$", "", StringComparison.OrdinalIgnoreCase)
                        .Replace(" ", "").Replace(" ", "");
        return decimal.TryParse(clean,
            NumberStyles.AllowLeadingSign | NumberStyles.AllowThousands | NumberStyles.AllowDecimalPoint,
            PtBr, out amount);
    }
}
