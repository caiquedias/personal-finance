using System.Text.Encodings.Web;
using System.Text.Json;

namespace PersonalFinance.Application.Services.Audit;

/// <summary>
/// Serializa o campo Details da auditoria como JSON (máx. 2000 chars).
/// Acima do limite, descarta chaves excedentes e sinaliza com "truncated": true, mantendo JSON válido.
/// </summary>
public static class AuditDetailsSerializer
{
    public const int MaxLength = 2000;

    private static readonly JsonSerializerOptions Options = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static string Serialize(IReadOnlyDictionary<string, object?> details)
    {
        var full = JsonSerializer.Serialize(details, Options);
        if (full.Length <= MaxLength)
            return full;

        // Mantém as chaves, na ordem, que couberem junto com a flag truncated
        var kept = new Dictionary<string, object?>();
        foreach (var (key, value) in details)
        {
            var candidate = new Dictionary<string, object?>(kept) { [key] = value, ["truncated"] = true };
            if (JsonSerializer.Serialize(candidate, Options).Length > MaxLength)
                continue;
            kept[key] = value;
        }

        kept["truncated"] = true;
        return JsonSerializer.Serialize(kept, Options);
    }
}
