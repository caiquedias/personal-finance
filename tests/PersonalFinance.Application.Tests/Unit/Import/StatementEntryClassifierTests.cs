using FluentAssertions;
using PersonalFinance.Application.UseCases.Import;
using PersonalFinance.Domain.Entities.Config;
using Xunit;

namespace PersonalFinance.Application.Tests.Unit.Import;

public class StatementEntryClassifierTests
{
    private readonly StatementEntryClassifier _sut = new();

    private static readonly Guid UserId = Guid.NewGuid();

    // ── Transferência interna ─────────────────────────────────────────────────

    [Theory(DisplayName = "Deve detectar transferência interna")]
    [InlineData("PGTO FAT CARTAO C6")]
    [InlineData("pgto fat cartao c6")]
    [InlineData("APLICAÇÃO CDB")]
    [InlineData("APLICACAO CDB")]
    [InlineData("aplicacao automatica")]
    [InlineData("RESGATE CDB DI")]
    [InlineData("resgate cdb")]
    public void IsInternalTransfer_KnownPatterns_ShouldBeTrue(string description) =>
        _sut.IsInternalTransfer(description).Should().BeTrue();

    [Theory(DisplayName = "Não deve marcar como transferência interna")]
    [InlineData("UBER TRIP")]
    [InlineData("RESGATE POUPANCA")]   // RESGATE sem CDB não é transferência
    [InlineData("PIX RECEBIDO JOAO")]
    [InlineData("")]
    public void IsInternalTransfer_OtherDescriptions_ShouldBeFalse(string description) =>
        _sut.IsInternalTransfer(description).Should().BeFalse();

    // ── Categoria por palavra-chave ───────────────────────────────────────────

    [Fact(DisplayName = "Deve sugerir categoria pelo nome mapeado à palavra-chave")]
    public void SuggestCategoryId_KeywordMatch_ShouldReturnCategoryId()
    {
        var transport = Category.CreateGlobal("Transporte", "#111111", null);
        var food      = Category.CreateGlobal("Alimentação", "#222222", null);

        _sut.SuggestCategoryId("UBER *TRIP 123", new[] { transport, food }).Should().Be(transport.Id);
        _sut.SuggestCategoryId("ifood pedido",   new[] { transport, food }).Should().Be(food.Id);
    }

    [Fact(DisplayName = "Deve sugerir categoria do próprio usuário quando nome coincide")]
    public void SuggestCategoryId_UserCategory_ShouldMatchByName()
    {
        var market = Category.Create("Mercado", "#333333", null, UserId);

        _sut.SuggestCategoryId("SUPERMERCADO EXTRA", new[] { market }).Should().Be(market.Id);
    }

    [Fact(DisplayName = "Deve retornar null quando nenhuma palavra-chave casa")]
    public void SuggestCategoryId_NoKeyword_ShouldReturnNull()
    {
        var transport = Category.CreateGlobal("Transporte", "#111111", null);

        _sut.SuggestCategoryId("LOJA XYZ", new[] { transport }).Should().BeNull();
    }

    [Fact(DisplayName = "Deve retornar null quando categoria mapeada não está na lista acessível")]
    public void SuggestCategoryId_CategoryNotAccessible_ShouldReturnNull()
    {
        var other = Category.CreateGlobal("Lazer", "#444444", null);

        _sut.SuggestCategoryId("UBER TRIP", new[] { other }).Should().BeNull();
    }
}
