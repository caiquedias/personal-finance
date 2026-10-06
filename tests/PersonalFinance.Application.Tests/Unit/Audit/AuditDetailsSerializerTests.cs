using FluentAssertions;
using PersonalFinance.Application.Services.Audit;
using System.Text.Json;
using Xunit;

namespace PersonalFinance.Application.Tests.Unit.Audit;

/// <summary>Serializer de Details da auditoria: JSON válido, máx. 2000 chars, truncamento sinalizado — #402.</summary>
public class AuditDetailsSerializerTests
{
    [Fact(DisplayName = "MaxLength deve ser 2000")]
    public void MaxLength_ShouldBe2000() => AuditDetailsSerializer.MaxLength.Should().Be(2000);

    [Fact(DisplayName = "Deve serializar dicionário pequeno como JSON válido sem flag truncated")]
    public void Serialize_Small_ShouldBeValidJson()
    {
        var json = AuditDetailsSerializer.Serialize(new Dictionary<string, object?> { ["roleId"] = 2, ["note"] = "ok" });

        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("roleId").GetInt32().Should().Be(2);
        doc.RootElement.GetProperty("note").GetString().Should().Be("ok");
        doc.RootElement.TryGetProperty("truncated", out _).Should().BeFalse();
    }

    [Fact(DisplayName = "Dicionário vazio deve gerar JSON objeto válido")]
    public void Serialize_Empty_ShouldBeValidJson()
    {
        var json = AuditDetailsSerializer.Serialize(new Dictionary<string, object?>());

        using var doc = JsonDocument.Parse(json);
        doc.RootElement.ValueKind.Should().Be(JsonValueKind.Object);
    }

    [Fact(DisplayName = "Conteúdo grande deve ser truncado em até 2000 chars, JSON válido e truncated=true")]
    public void Serialize_Large_ShouldTruncateKeepingValidJson()
    {
        var json = AuditDetailsSerializer.Serialize(new Dictionary<string, object?>
        {
            ["roleId"] = 1,
            ["big"] = new string('x', 5000)
        });

        json.Length.Should().BeLessThanOrEqualTo(2000);
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("truncated").GetBoolean().Should().BeTrue();
    }

    [Fact(DisplayName = "Muitas chaves pequenas somando mais de 2000 chars também truncam com JSON válido")]
    public void Serialize_ManyKeys_ShouldTruncateKeepingValidJson()
    {
        var dict = Enumerable.Range(0, 400).ToDictionary(i => $"k{i}", i => (object?)new string('v', 20));

        var json = AuditDetailsSerializer.Serialize(dict);

        json.Length.Should().BeLessThanOrEqualTo(2000);
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("truncated").GetBoolean().Should().BeTrue();
    }

    [Fact(DisplayName = "Caracteres especiais e acentos devem permanecer em JSON válido")]
    public void Serialize_SpecialChars_ShouldRoundTrip()
    {
        var json = AuditDetailsSerializer.Serialize(new Dictionary<string, object?> { ["note"] = "aspas \" e ação \\ ok" });

        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("note").GetString().Should().Be("aspas \" e ação \\ ok");
    }

    [Fact(DisplayName = "Só serializa as chaves recebidas (nenhum campo sensível inventado)")]
    public void Serialize_ShouldOnlyContainGivenKeys()
    {
        var json = AuditDetailsSerializer.Serialize(new Dictionary<string, object?> { ["roleId"] = 1 });

        using var doc = JsonDocument.Parse(json);
        doc.RootElement.EnumerateObject().Select(p => p.Name).Should().Equal("roleId");
    }
}
