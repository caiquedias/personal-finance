using FluentAssertions;
using PersonalFinance.Domain.Entities.Auth;
using PersonalFinance.Domain.Enums;
using PersonalFinance.Domain.Exceptions;
using Xunit;

namespace PersonalFinance.Domain.Tests.Unit.Auth;

/// <summary>Testes da entidade AuditLog (trilha de auditoria insert-only) — #402.</summary>
public class AuditLogTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Actor = Guid.NewGuid();
    private static readonly Guid Target = Guid.NewGuid();

    [Fact(DisplayName = "Create deve preencher todos os campos e gerar Id")]
    public void Create_ShouldFillAllFields()
    {
        var log = AuditLog.Create(Actor, AuditAction.RoleAssigned, Target, "{\"roleId\":1}", "203.0.113.7", Now);

        log.Id.Should().NotBeEmpty();
        log.ActorUserId.Should().Be(Actor);
        log.TargetUserId.Should().Be(Target);
        log.Action.Should().Be(AuditAction.RoleAssigned);
        log.Details.Should().Be("{\"roleId\":1}");
        log.IpAddress.Should().Be("203.0.113.7");
        log.CreatedAt.Should().Be(Now);
    }

    [Fact(DisplayName = "Create deve gerar Ids distintos")]
    public void Create_ShouldGenerateDistinctIds()
    {
        var a = AuditLog.Create(Actor, AuditAction.MfaReset, Target, null, null, Now);
        var b = AuditLog.Create(Actor, AuditAction.MfaReset, Target, null, null, Now);

        a.Id.Should().NotBe(b.Id);
    }

    [Fact(DisplayName = "Details e IpAddress são opcionais")]
    public void Create_WithNullOptionals_ShouldSucceed()
    {
        var log = AuditLog.Create(Actor, AuditAction.UserCreated, Target, null, null, Now);

        log.Details.Should().BeNull();
        log.IpAddress.Should().BeNull();
    }

    [Fact(DisplayName = "Create deve rejeitar ator Guid.Empty")]
    public void Create_EmptyActor_ShouldThrow()
    {
        var act = () => AuditLog.Create(Guid.Empty, AuditAction.UserCreated, Target, null, null, Now);
        act.Should().Throw<DomainException>();
    }

    [Fact(DisplayName = "Create deve rejeitar alvo Guid.Empty")]
    public void Create_EmptyTarget_ShouldThrow()
    {
        var act = () => AuditLog.Create(Actor, AuditAction.UserCreated, Guid.Empty, null, null, Now);
        act.Should().Throw<DomainException>();
    }

    [Fact(DisplayName = "Details com exatamente 2000 caracteres deve ser aceito")]
    public void Create_Details2000_ShouldSucceed()
    {
        var act = () => AuditLog.Create(Actor, AuditAction.UserUpdated, Target, new string('a', 2000), null, Now);
        act.Should().NotThrow();
    }

    [Fact(DisplayName = "Details com mais de 2000 caracteres deve ser rejeitado")]
    public void Create_Details2001_ShouldThrow()
    {
        var act = () => AuditLog.Create(Actor, AuditAction.UserUpdated, Target, new string('a', 2001), null, Now);
        act.Should().Throw<DomainException>();
    }

    [Fact(DisplayName = "IpAddress com 45 caracteres é aceito e com 46 é rejeitado")]
    public void Create_IpLimits()
    {
        var ok = () => AuditLog.Create(Actor, AuditAction.UserUpdated, Target, null, new string('1', 45), Now);
        var bad = () => AuditLog.Create(Actor, AuditAction.UserUpdated, Target, null, new string('1', 46), Now);

        ok.Should().NotThrow();
        bad.Should().Throw<DomainException>();
    }

    [Fact(DisplayName = "AuditAction deve ter os 8 valores previstos")]
    public void AuditAction_ShouldContainExpectedValues()
    {
        Enum.GetNames<AuditAction>().Should().BeEquivalentTo(
            "UserCreated", "UserUpdated", "UserActivated", "UserDeactivated",
            "RoleAssigned", "RoleRemoved", "PasswordReset", "MfaReset");
    }

    [Fact(DisplayName = "AuditLog não herda EntityBase (insert-only, sem soft-delete)")]
    public void AuditLog_ShouldNotBeSoftDeletable()
    {
        typeof(AuditLog).GetProperty("DeletedAt").Should().BeNull();
        typeof(AuditLog).BaseType.Should().Be(typeof(object));
    }
}
