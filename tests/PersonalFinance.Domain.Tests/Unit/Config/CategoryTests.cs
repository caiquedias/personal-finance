using FluentAssertions;
using PersonalFinance.Domain.Entities.Config;
using PersonalFinance.Domain.Exceptions;
using Xunit;

namespace PersonalFinance.Domain.Tests.Unit.Config;

/// <summary>
/// Testes da entidade Category.
/// Categoria pode ser global (UserId nulo) ou pertencer a um usuário específico.
/// </summary>
public class CategoryTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    // ── Criação válida ────────────────────────────────────────────────────────

    [Fact(DisplayName = "Deve criar categoria de usuário com dados válidos")]
    public void Create_UserCategory_ShouldSucceed()
    {
        var category = Category.Create("Moradia", "#1E4D2B", "home", UserId);

        category.Name.Should().Be("Moradia");
        category.Color.Should().Be("#1E4D2B");
        category.Icon.Should().Be("home");
        category.UserId.Should().Be(UserId);
        category.IsGlobal.Should().BeFalse();
        category.IsActive.Should().BeTrue();
    }

    [Fact(DisplayName = "Deve criar categoria global sem UserId")]
    public void Create_GlobalCategory_ShouldHaveNullUserId()
    {
        var category = Category.CreateGlobal("Alimentação", "#8B5E3C", "food");

        category.UserId.Should().BeNull();
        category.IsGlobal.Should().BeTrue();
        category.IsActive.Should().BeTrue();
    }

    [Fact(DisplayName = "Icon pode ser nulo")]
    public void Create_WithNullIcon_ShouldSucceed()
    {
        var category = Category.Create("Transporte", "#FF0000", null, UserId);
        category.Icon.Should().BeNull();
    }

    // ── Validações de nome ────────────────────────────────────────────────────

    [Theory(DisplayName = "Deve lançar exceção para nome inválido")]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null)]
    public void Create_WithInvalidName_ShouldThrow(string? name)
    {
        var act = () => Category.Create(name!, "#FFFFFF", null, UserId);
        act.Should().Throw<DomainException>()
           .WithMessage("*nome*");
    }

    [Fact(DisplayName = "Deve lançar exceção para nome com mais de 100 caracteres")]
    public void Create_WithNameTooLong_ShouldThrow()
    {
        var name = new string('A', 101);
        var act  = () => Category.Create(name, "#FFFFFF", null, UserId);
        act.Should().Throw<DomainException>();
    }

    // ── Validações de cor ─────────────────────────────────────────────────────

    [Theory(DisplayName = "Deve lançar exceção para cor em formato inválido")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("FFFFFF")]        // sem #
    [InlineData("#GGGGGG")]       // caracteres inválidos
    [InlineData("#FFF")]          // shorthand não suportado
    [InlineData("#FFFFFFFF")]     // rgba não suportado
    public void Create_WithInvalidColor_ShouldThrow(string? color)
    {
        var act = () => Category.Create("Test", color!, null, UserId);
        act.Should().Throw<DomainException>()
           .WithMessage("*cor*");
    }

    [Theory(DisplayName = "Deve aceitar cor em formato hex válido")]
    [InlineData("#1E4D2B")]
    [InlineData("#FFFFFF")]
    [InlineData("#000000")]
    [InlineData("#aabbcc")]  // lowercase aceito
    public void Create_WithValidColor_ShouldSucceed(string color)
    {
        var act = () => Category.Create("Test", color, null, UserId);
        act.Should().NotThrow();
    }

    // ── Atualização ───────────────────────────────────────────────────────────

    [Fact(DisplayName = "Update deve alterar nome, cor e ícone")]
    public void Update_ShouldChangeFields()
    {
        var category = Category.Create("Moradia", "#1E4D2B", "home", UserId);
        category.Update("Casa", "#FFFFFF", "house");

        category.Name.Should().Be("Casa");
        category.Color.Should().Be("#FFFFFF");
        category.Icon.Should().Be("house");
    }

    [Fact(DisplayName = "Update deve atualizar UpdatedAt")]
    public void Update_ShouldUpdateTimestamp()
    {
        var category = Category.Create("Moradia", "#1E4D2B", "home", UserId);
        var original = category.UpdatedAt;

        Task.Delay(10).Wait();
        category.Update("Casa", "#FFFFFF", null);

        category.UpdatedAt.Should().BeAfter(original);
    }

    // ── Soft delete ───────────────────────────────────────────────────────────

    [Fact(DisplayName = "SoftDelete deve desativar a categoria")]
    public void SoftDelete_ShouldDeactivateCategory()
    {
        var category = Category.Create("Moradia", "#1E4D2B", "home", UserId);
        category.SoftDelete();

        category.IsActive.Should().BeFalse();
        category.IsDeleted.Should().BeTrue();
    }

    // ── Global category rules ─────────────────────────────────────────────────

    [Fact(DisplayName = "Categoria global não pode ter UserId definido")]
    public void CreateGlobal_ShouldNotHaveUserId()
    {
        var category = Category.CreateGlobal("Global Cat", "#FFFFFF", null);
        category.UserId.Should().BeNull();
        category.IsGlobal.Should().BeTrue();
    }
}
