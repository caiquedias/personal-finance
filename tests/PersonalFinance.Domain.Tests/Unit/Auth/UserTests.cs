using FluentAssertions;
using PersonalFinance.Domain.Entities.Auth;
using PersonalFinance.Domain.Exceptions;
using Xunit;

namespace PersonalFinance.Domain.Tests.Unit.Auth;

/// <summary>
/// Testes da entidade User.
/// Senhas não são validadas aqui — responsabilidade do serviço de Auth (Argon2id).
/// </summary>
public class UserTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────
    private static User CreateValid() =>
        User.Create("Caique Dias", "caique@monkeybomb.com", "hashed_password_argon2");

    // ── Criação válida ────────────────────────────────────────────────────────

    [Fact(DisplayName = "Deve criar usuário com dados válidos")]
    public void Create_WithValidData_ShouldSucceed()
    {
        var user = CreateValid();

        user.Name.Should().Be("Caique Dias");
        user.Email.Should().Be("caique@monkeybomb.com");
        user.PasswordHash.Should().Be("hashed_password_argon2");
        user.IsActive.Should().BeTrue();
        user.Id.Should().NotBeEmpty();
    }

    [Fact(DisplayName = "E-mail deve ser normalizado para lowercase")]
    public void Create_EmailShouldBeNormalizedToLowercase()
    {
        var user = User.Create("Caique", "CAIQUE@MonkeyBomb.COM", "hash");
        user.Email.Should().Be("caique@monkeybomb.com");
    }

    // ── Validações de nome ────────────────────────────────────────────────────

    [Theory(DisplayName = "Deve lançar exceção para nome inválido")]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null)]
    public void Create_WithInvalidName_ShouldThrow(string? name)
    {
        var act = () => User.Create(name!, "caique@monkeybomb.com", "hash");
        act.Should().Throw<DomainException>()
           .WithMessage("*nome*");
    }

    [Fact(DisplayName = "Deve lançar exceção para nome com mais de 100 caracteres")]
    public void Create_WithNameTooLong_ShouldThrow()
    {
        var name = new string('A', 101);
        var act  = () => User.Create(name, "caique@monkeybomb.com", "hash");
        act.Should().Throw<DomainException>();
    }

    // ── Validações de e-mail ──────────────────────────────────────────────────

    [Theory(DisplayName = "Deve lançar exceção para e-mail inválido")]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null)]
    [InlineData("nao_e_um_email")]
    [InlineData("@semdominio.com")]
    [InlineData("semdominio@")]
    public void Create_WithInvalidEmail_ShouldThrow(string? email)
    {
        var act = () => User.Create("Caique", email!, "hash");
        act.Should().Throw<DomainException>()
           .WithMessage("*e-mail*");
    }

    [Fact(DisplayName = "Deve lançar exceção para e-mail com mais de 200 caracteres")]
    public void Create_WithEmailTooLong_ShouldThrow()
    {
        var email = new string('a', 201) + "@x.com";
        var act   = () => User.Create("Caique", email, "hash");
        act.Should().Throw<DomainException>();
    }

    // ── Validações de senha ───────────────────────────────────────────────────

    [Theory(DisplayName = "Deve lançar exceção para hash de senha inválido")]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null)]
    public void Create_WithInvalidPasswordHash_ShouldThrow(string? hash)
    {
        var act = () => User.Create("Caique", "caique@monkeybomb.com", hash!);
        act.Should().Throw<DomainException>()
           .WithMessage("*senha*");
    }

    // ── Atualização ───────────────────────────────────────────────────────────

    [Fact(DisplayName = "UpdateName deve atualizar o nome e o UpdatedAt")]
    public void UpdateName_ShouldUpdateNameAndTimestamp()
    {
        var user     = CreateValid();
        var original = user.UpdatedAt;

        Task.Delay(10).Wait();
        user.UpdateName("Novo Nome");

        user.Name.Should().Be("Novo Nome");
        user.UpdatedAt.Should().BeAfter(original);
    }

    [Theory(DisplayName = "UpdateName deve lançar exceção para nome inválido")]
    [InlineData("")]
    [InlineData(null)]
    public void UpdateName_WithInvalidName_ShouldThrow(string? name)
    {
        var user = CreateValid();
        var act  = () => user.UpdateName(name!);
        act.Should().Throw<DomainException>();
    }

    [Fact(DisplayName = "UpdatePasswordHash deve atualizar o hash")]
    public void UpdatePasswordHash_ShouldUpdateHash()
    {
        var user = CreateValid();
        user.UpdatePasswordHash("new_argon2_hash");
        user.PasswordHash.Should().Be("new_argon2_hash");
    }

    // ── Soft delete ───────────────────────────────────────────────────────────

    [Fact(DisplayName = "SoftDelete deve desativar o usuário")]
    public void SoftDelete_ShouldDeactivateUser()
    {
        var user = CreateValid();
        user.SoftDelete();

        user.IsActive.Should().BeFalse();
        user.IsDeleted.Should().BeTrue();
        user.DeletedAt.Should().NotBeNull();
    }
}
