using FluentAssertions;
using PersonalFinance.Domain.Entities.Auth;
using PersonalFinance.Domain.Exceptions;
using Xunit;

namespace PersonalFinance.Domain.Tests.Unit.Auth;

/// <summary>
/// Testes da entidade MfaRecoveryCode (código de recuperação de uso único) — #393.
/// Só o hash é guardado; o código em claro nunca chega à entidade.
/// </summary>
public class MfaRecoveryCodeTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact(DisplayName = "Create deve gerar código não usado, ativo e com Id novo")]
    public void Create_ShouldInitializeUnusedCode()
    {
        var userId = Guid.NewGuid();

        var code = MfaRecoveryCode.Create(userId, "hash-1");

        code.Id.Should().NotBeEmpty();
        code.UserId.Should().Be(userId);
        code.CodeHash.Should().Be("hash-1");
        code.UsedAt.Should().BeNull();
        code.IsUsed.Should().BeFalse();
        code.IsDeleted.Should().BeFalse();
    }

    [Fact(DisplayName = "Create com UserId vazio deve lançar DomainException")]
    public void Create_WithEmptyUserId_ShouldThrow()
    {
        var act = () => MfaRecoveryCode.Create(Guid.Empty, "hash-1");

        act.Should().Throw<DomainException>();
    }

    [Theory(DisplayName = "Create com hash vazio deve lançar DomainException")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Create_WithBlankHash_ShouldThrow(string? hash)
    {
        var act = () => MfaRecoveryCode.Create(Guid.NewGuid(), hash!);

        act.Should().Throw<DomainException>();
    }

    [Fact(DisplayName = "MarkUsed deve registrar UsedAt e marcar como usado")]
    public void MarkUsed_ShouldSetUsedAt()
    {
        var code = MfaRecoveryCode.Create(Guid.NewGuid(), "hash-1");

        code.MarkUsed(Now);

        code.UsedAt.Should().Be(Now);
        code.IsUsed.Should().BeTrue();
    }

    [Fact(DisplayName = "MarkUsed em código já usado deve lançar DomainException e manter UsedAt original")]
    public void MarkUsed_WhenAlreadyUsed_ShouldThrow()
    {
        var code = MfaRecoveryCode.Create(Guid.NewGuid(), "hash-1");
        code.MarkUsed(Now);

        var act = () => code.MarkUsed(Now.AddMinutes(5));

        act.Should().Throw<DomainException>();
        code.UsedAt.Should().Be(Now);
    }
}
