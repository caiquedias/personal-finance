using System.Globalization;
using System.Text;
using PersonalFinance.Domain.Entities.Config;

namespace PersonalFinance.Application.UseCases.Import;

/// <summary>
/// Classifica lançamentos de extrato: detecção de transferência interna e
/// sugestão de categoria por palavra-chave.
/// </summary>
public sealed class StatementEntryClassifier
{
    // Palavra-chave (normalizada) → nome da categoria
    private static readonly (string Keyword, string CategoryName)[] KeywordMap =
    [
        ("UBER",         "Transporte"),
        ("99POP",        "Transporte"),
        ("IFOOD",        "Alimentação"),
        ("RESTAURANTE",  "Alimentação"),
        ("SUPERMERCADO", "Mercado"),
        ("MERCADO",      "Mercado"),
        ("FARMACIA",     "Saúde"),
        ("DROGARIA",     "Saúde"),
        ("NETFLIX",      "Lazer"),
        ("SPOTIFY",      "Lazer"),
    ];

    public bool IsInternalTransfer(string description)
    {
        var text = Normalize(description);
        if (text.Length == 0) return false;

        return text.Contains("PGTO FAT CARTAO")
            || text.Contains("APLICACAO")
            || (text.Contains("RESGATE") && text.Contains("CDB"));
    }

    public Guid? SuggestCategoryId(string description, IEnumerable<Category> accessibleCategories)
    {
        var text = Normalize(description);
        if (text.Length == 0) return null;

        var categories = accessibleCategories.ToList();
        foreach (var (keyword, categoryName) in KeywordMap)
        {
            if (!text.Contains(keyword)) continue;

            var match = categories.FirstOrDefault(c =>
                string.Equals(Normalize(c.Name), Normalize(categoryName), StringComparison.Ordinal));
            if (match is not null) return match.Id;
        }

        return null;
    }

    // Remove acentos e padroniza para maiúsculas
    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        var decomposed = value.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                sb.Append(ch);
        }
        return sb.ToString().Normalize(NormalizationForm.FormC).ToUpperInvariant();
    }
}
