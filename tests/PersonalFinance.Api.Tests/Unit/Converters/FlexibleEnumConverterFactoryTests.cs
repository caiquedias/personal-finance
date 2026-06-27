using FluentAssertions;
using PersonalFinance.Api.Converters;
using PersonalFinance.Domain.Enums;
using System.Text.Json;
using Xunit;

namespace PersonalFinance.Api.Tests.Unit.Converters;

/// <summary>
/// Testes unitários da FlexibleEnumConverterFactory.
/// Valida desserialização flexível (int, string numérica, nome) e serialização como int.
/// </summary>
public class FlexibleEnumConverterFactoryTests
{
    private readonly JsonSerializerOptions _options;

    public FlexibleEnumConverterFactoryTests()
    {
        _options = new JsonSerializerOptions();
        _options.Converters.Add(new FlexibleEnumConverterFactory());
    }

    // ── Desserialização ──────────────────────────────────────────────────────────

    [Fact(DisplayName = "Deserializa integer 1 → FortnightType.First")]
    public void Read_IntegerValue_ShouldDeserializeCorrectly()
    {
        // Arrange
        const string json = "1";

        // Act
        var result = JsonSerializer.Deserialize<FortnightType>(json, _options);

        // Assert
        result.Should().Be(FortnightType.First);
    }

    [Fact(DisplayName = "Deserializa string numérica \"1\" → FortnightType.First")]
    public void Read_NumericStringValue_ShouldDeserializeCorrectly()
    {
        // Arrange
        const string json = "\"1\"";

        // Act
        var result = JsonSerializer.Deserialize<FortnightType>(json, _options);

        // Assert
        result.Should().Be(FortnightType.First);
    }

    [Fact(DisplayName = "Deserializa string pelo nome exato \"First\" → FortnightType.First")]
    public void Read_NamedStringValue_ShouldDeserializeCorrectly()
    {
        // Arrange
        const string json = "\"First\"";

        // Act
        var result = JsonSerializer.Deserialize<FortnightType>(json, _options);

        // Assert
        result.Should().Be(FortnightType.First);
    }

    [Fact(DisplayName = "Deserializa string pelo nome em minúsculas \"first\" → FortnightType.First (case-insensitive)")]
    public void Read_LowercaseNamedStringValue_ShouldDeserializeCaseInsensitively()
    {
        // Arrange
        const string json = "\"first\"";

        // Act
        var result = JsonSerializer.Deserialize<FortnightType>(json, _options);

        // Assert
        result.Should().Be(FortnightType.First);
    }

    [Fact(DisplayName = "Valor desconhecido → JsonException")]
    public void Read_UnknownValue_ShouldThrowJsonException()
    {
        // Arrange
        const string json = "\"InvalidEnumValue\"";

        // Act
        var act = () => JsonSerializer.Deserialize<FortnightType>(json, _options);

        // Assert
        act.Should().Throw<JsonException>();
    }

    // ── Serialização ─────────────────────────────────────────────────────────────

    [Fact(DisplayName = "Serializa FortnightType.First → integer 1 (não string)")]
    public void Write_EnumValue_ShouldSerializeAsInteger()
    {
        // Arrange
        var value = FortnightType.First;

        // Act
        var json = JsonSerializer.Serialize(value, _options);

        // Assert
        // Deve serializar como número, não como string
        json.Should().Be("1");
    }
}
