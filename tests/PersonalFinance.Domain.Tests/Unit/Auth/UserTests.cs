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

    // ── Lockout de login (#391) ───────────────────────────────────────────────

    private static readonly DateTime Now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan Duration = TimeSpan.FromMinutes(15);

    [Fact(DisplayName = "Novo usuário não deve ter falhas nem bloqueio")]
    public void Create_ShouldStartWithoutFailedLogins()
    {
        var user = CreateValid();

        user.FailedLoginCount.Should().Be(0);
        user.LockedUntil.Should().BeNull();
        user.IsLockedOut(Now).Should().BeFalse();
    }

    [Fact(DisplayName = "RegisterFailedLogin deve incrementar o contador sem bloquear abaixo do limite")]
    public void RegisterFailedLogin_BelowLimit_ShouldIncrementWithoutLocking()
    {
        var user = CreateValid();

        user.RegisterFailedLogin(5, Duration, Now);
        user.RegisterFailedLogin(5, Duration, Now);

        user.FailedLoginCount.Should().Be(2);
        user.LockedUntil.Should().BeNull();
        user.IsLockedOut(Now).Should().BeFalse();
    }

    [Fact(DisplayName = "RegisterFailedLogin deve bloquear ao atingir o limite com LockedUntil = now + duração")]
    public void RegisterFailedLogin_AtLimit_ShouldLockUntilNowPlusDuration()
    {
        var user = CreateValid();

        for (var i = 0; i < 5; i++)
            user.RegisterFailedLogin(5, Duration, Now);

        user.LockedUntil.Should().Be(Now + Duration);
        user.IsLockedOut(Now).Should().BeTrue();
    }

    [Fact(DisplayName = "IsLockedOut deve ser true dentro da janela e false após expirar")]
    public void IsLockedOut_ShouldDependOnWindow()
    {
        var user = CreateValid();
        for (var i = 0; i < 5; i++)
            user.RegisterFailedLogin(5, Duration, Now);

        user.IsLockedOut(Now.AddMinutes(14)).Should().BeTrue();
        user.IsLockedOut(Now.AddMinutes(15)).Should().BeFalse();
        user.IsLockedOut(Now.AddMinutes(16)).Should().BeFalse();
    }

    [Fact(DisplayName = "ResetFailedLogins deve zerar contador e LockedUntil")]
    public void ResetFailedLogins_ShouldClearCounterAndLock()
    {
        var user = CreateValid();
        for (var i = 0; i < 5; i++)
            user.RegisterFailedLogin(5, Duration, Now);

        user.ResetFailedLogins();

        user.FailedLoginCount.Should().Be(0);
        user.LockedUntil.Should().BeNull();
        user.IsLockedOut(Now).Should().BeFalse();
    }

    [Fact(DisplayName = "Após expirar o lockout o contador deve recomeçar do zero")]
    public void RegisterFailedLogin_AfterLockoutExpired_ShouldRestartCounter()
    {
        var user = CreateValid();
        for (var i = 0; i < 5; i++)
            user.RegisterFailedLogin(5, Duration, Now);

        var later = Now.AddMinutes(16);
        user.RegisterFailedLogin(5, Duration, later);

        user.FailedLoginCount.Should().Be(1);
        user.LockedUntil.Should().BeNull();
        user.IsLockedOut(later).Should().BeFalse();
    }
}
