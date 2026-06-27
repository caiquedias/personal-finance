using System.Text.Json;
using System.Text.Json.Serialization;

namespace PersonalFinance.Api.Converters;

/// <summary>
/// Factory que registra um converter JSON flexível para todos os tipos enum.
/// Aceita valores como inteiro, string numérica ou nome do enum (case-insensitive).
/// </summary>
public sealed class FlexibleEnumConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
        => typeToConvert.IsEnum;

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var converterType = typeof(FlexibleEnumConverter<>).MakeGenericType(typeToConvert);
        return (JsonConverter)Activator.CreateInstance(converterType)!;
    }

    // ── Converter genérico interno ────────────────────────────────────────────────
    private sealed class FlexibleEnumConverter<TEnum> : JsonConverter<TEnum>
        where TEnum : struct, Enum
    {
        public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Number)
            {
                return (TEnum)(object)reader.GetInt32();
            }

            if (reader.TokenType == JsonTokenType.String)
            {
                var str = reader.GetString()!;

                // Tenta parse numérico
                if (int.TryParse(str, out int n))
                    return (TEnum)(object)n;

                // Tenta parse pelo nome (case-insensitive)
                if (Enum.TryParse<TEnum>(str, ignoreCase: true, out var val))
                    return val;

                throw new JsonException($"Valor '{str}' não é válido para o enum {typeof(TEnum).Name}.");
            }

            throw new JsonException($"Token inesperado '{reader.TokenType}' ao deserializar {typeof(TEnum).Name}.");
        }

        public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options)
            => writer.WriteNumberValue(Convert.ToInt32(value));
    }
}
