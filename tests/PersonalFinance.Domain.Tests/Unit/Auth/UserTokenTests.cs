using FluentAssertions;
using PersonalFinance.Domain.Entities.Auth;
using PersonalFinance.Domain.Enums;
using PersonalFinance.Domain.Exceptions;
using Xunit;

namespace PersonalFinance.Domain.Tests.Unit.Auth;

/// <summary>
/// Testes da entidade UserToken (#404): código de uso único de reset de senha / verificação de e-mail.
/// Só o hash é guardado. Máx. de tentativas erradas invalida o token; TTL; uso único.
/// Não herda EntityBase (hard delete dos anteriores a cada emissão).
/// </summary>
public class UserTokenTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);

    private static UserToken NewToken(UserTokenPurpose purpose = UserTokenPurpose.PasswordReset)
    {
        var token = UserToken.Create(Guid.NewGuid(), purpose, Now, Ttl);
        token.SetTokenHash("hash-1");
        return token;
    }

    // ── Enum ──────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "UserTokenPurpose deve ter PasswordReset=1 e EmailVerification=2")]
    public void Purpose_ShouldHaveStableValues()
    {
        ((int)UserTokenPurpose.PasswordReset).Should().Be(1);
        ((int)UserTokenPurpose.EmailVerification).Should().Be(2);
        Enum.GetNames<UserTokenPurpose>().Should().BeEquivalentTo("PasswordReset", "EmailVerification");
    }

    // ── Create ────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "Create deve inicializar token não usado, sem tentativas e com expiração = now + ttl")]
    public void Create_ShouldInitializeFreshToken()
    {
        var userId = Guid.NewGuid();

        var token = UserToken.Create(userId, UserTokenPurpose.EmailVerification, Now, Ttl);

        token.Id.Should().NotBeEmpty();
        token.UserId.Should().Be(userId);
        token.Purpose.Should().Be(UserTokenPurpose.EmailVerification);
        token.CreatedAt.Should().Be(Now);
        token.ExpiresAt.Should().Be(Now + Ttl);
        token.UsedAt.Should().BeNull();
        token.Attempts.Should().Be(0);
    }

    [Fact(DisplayName = "Dois tokens devem ter Ids distintos")]
    public void Create_TwoTokens_ShouldHaveDistinctIds()
    {
        var userId = Guid.NewGuid();

        UserToken.Create(userId, UserTokenPurpose.PasswordReset, Now, Ttl).Id
            .Should().NotBe(UserToken.Create(userId, UserTokenPurpose.PasswordReset, Now, Ttl).Id);
    }

    [Fact(DisplayName = "Create deve rejeitar usuário vazio")]
    public void Create_WithEmptyUser_ShouldThrow()
    {
        var act = () => UserToken.Create(Guid.Empty, UserTokenPurpose.PasswordReset, Now, Ttl);

        act.Should().Throw<DomainException>();
    }

    [Fact(DisplayName = "Create deve rejeitar propósito indefinido")]
    public void Create_WithUndefinedPurpose_ShouldThrow()
    {
        var act = () => UserToken.Create(Guid.NewGuid(), (UserTokenPurpose)99, Now, Ttl);

        act.Should().Throw<DomainException>();
    }

    [Theory(DisplayName = "Create deve rejeitar TTL zero ou negativo")]
    [InlineData(0)]
    [InlineData(-5)]
    public void Create_WithNonPositiveTtl_ShouldThrow(int minutes)
    {
        var act = () => UserToken.Create(Guid.NewGuid(), UserTokenPurpose.PasswordReset, Now, TimeSpan.FromMinutes(minutes));

        act.Should().Throw<DomainException>();
    }

    // ── Hash ──────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "SetTokenHash deve guardar o hash")]
    public void SetTokenHash_ShouldStoreHash()
    {
        var token = UserToken.Create(Guid.NewGuid(), UserTokenPurpose.PasswordReset, Now, Ttl);

        token.SetTokenHash("abc");

        token.TokenHash.Should().Be("abc");
    }

    [Theory(DisplayName = "SetTokenHash deve rejeitar hash vazio")]
    [InlineData("")]
    [InlineData("   ")]
    public void SetTokenHash_WithEmpty_ShouldThrow(string hash)
    {
        var token = UserToken.Create(Guid.NewGuid(), UserTokenPurpose.PasswordReset, Now, Ttl);

        var act = () => token.SetTokenHash(hash);

        act.Should().Throw<DomainException>();
    }

    // ── IsUsable (TTL / uso) ──────────────────────────────────────────────────

    [Fact(DisplayName = "Token recém-criado é utilizável dentro do TTL")]
    public void IsUsable_WithinTtl_ShouldBeTrue()
    {
        NewToken().IsUsable(Now.AddMinutes(9)).Should().BeTrue();
    }

    [Fact(DisplayName = "Token expira exatamente em ExpiresAt (now >= ExpiresAt não é utilizável)")]
    public void IsUsable_AtOrAfterExpiry_ShouldBeFalse()
    {
        var token = NewToken();

        token.IsUsable(Now + Ttl).Should().BeFalse();
        token.IsUsable(Now + Ttl + TimeSpan.FromSeconds(1)).Should().BeFalse();
    }

    [Fact(DisplayName = "MarkUsed deve registrar UsedAt e tornar o token não utilizável")]
    public void MarkUsed_ShouldSetUsedAtAndInvalidate()
    {
        var token = NewToken();

        token.MarkUsed(Now.AddMinutes(1));

        token.UsedAt.Should().Be(Now.AddMinutes(1));
        token.IsUsable(Now.AddMinutes(2)).Should().BeFalse();
    }

    [Fact(DisplayName = "MarkUsed de token já usado deve lançar (uso único)")]
    public void MarkUsed_Twice_ShouldThrow()
    {
        var token = NewToken();
        token.MarkUsed(Now.AddMinutes(1));

        var act = () => token.MarkUsed(Now.AddMinutes(2));

        act.Should().Throw<DomainException>();
    }

    // ── Tentativas ────────────────────────────────────────────────────────────

    [Fact(DisplayName = "RegisterFailedAttempt deve incrementar o contador sem invalidar abaixo do máximo")]
    public void RegisterFailedAttempt_BelowMax_ShouldIncrementAndKeepUsable()
    {
        var token = NewToken();

        token.RegisterFailedAttempt(3);
        token.RegisterFailedAttempt(3);

        token.Attempts.Should().Be(2);
        token.IsUsable(Now.AddMinutes(1)).Should().BeTrue();
    }

    [Fact(DisplayName = "A 3ª falha (máx 3) invalida o token")]
    public void RegisterFailedAttempt_ReachingMax_ShouldInvalidate()
    {
        var token = NewToken();

        token.RegisterFailedAttempt(3);
        token.RegisterFailedAttempt(3);
        token.RegisterFailedAttempt(3);

        token.Attempts.Should().Be(3);
        token.IsUsable(Now.AddMinutes(1)).Should().BeFalse();
    }

    [Fact(DisplayName = "Com máx 1, a primeira falha já invalida")]
    public void RegisterFailedAttempt_MaxOne_ShouldInvalidateImmediately()
    {
        var token = NewToken();

        token.RegisterFailedAttempt(1);

        token.IsUsable(Now.AddMinutes(1)).Should().BeFalse();
    }

    [Theory(DisplayName = "RegisterFailedAttempt deve rejeitar máximo menor que 1")]
    [InlineData(0)]
    [InlineData(-1)]
    public void RegisterFailedAttempt_WithInvalidMax_ShouldThrow(int max)
    {
        var token = NewToken();

        var act = () => token.RegisterFailedAttempt(max);

        act.Should().Throw<DomainException>();
    }

    // ── Estrutura ─────────────────────────────────────────────────────────────

    [Fact(DisplayName = "UserToken não herda EntityBase (hard delete, sem soft-delete)")]
    public void UserToken_ShouldNotBeSoftDeletable()
    {
        typeof(UserToken).GetProperty("DeletedAt").Should().BeNull();
        typeof(UserToken).BaseType.Should().Be(typeof(object));
    }

    [Fact(DisplayName = "UserToken não expõe o código em claro")]
    public void UserToken_ShouldNotExposePlainCode()
    {
        typeof(UserToken).GetProperties().Select(p => p.Name)
            .Should().NotContain(n => n.Equals("Code", StringComparison.OrdinalIgnoreCase));
    }
}
