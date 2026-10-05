using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using PersonalFinance.Domain.Entities.Auth;
using PersonalFinance.Domain.Enums;
using PersonalFinance.Infrastructure.Persistence.Context;
using Xunit;

namespace PersonalFinance.Infrastructure.Tests.Unit.Persistence;

/// <summary>Mapeamento EF de AuditLog (#402): insert-only, enum int, FKs Restrict, sem soft-delete.</summary>
public class AuditLogConfigurationTests
{
    private static IEntityType Entity()
    {
        var ctx = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"AuditCfg_{Guid.NewGuid()}").Options);
        return ctx.Model.FindEntityType(typeof(AuditLog))!;
    }

    [Fact(DisplayName = "AuditLog deve estar mapeada no modelo")]
    public void Entity_ShouldBeMapped() => Entity().Should().NotBeNull();

    [Fact(DisplayName = "PK Id deve ser ValueGeneratedNever")]
    public void Id_ShouldBeValueGeneratedNever()
    {
        var pk = Entity().FindPrimaryKey()!;

        pk.Properties.Single().Name.Should().Be("Id");
        pk.Properties.Single().ValueGenerated.Should().Be(ValueGenerated.Never);
    }

    [Fact(DisplayName = "Action deve ser convertida para int")]
    public void Action_ShouldConvertToInt()
    {
        var p = Entity().FindProperty("Action")!;

        (p.GetValueConverter()?.ProviderClrType ?? p.ClrType).Should().Be(typeof(int));
        p.IsNullable.Should().BeFalse();
    }

    [Fact(DisplayName = "Details deve ser nullable com até 2000 caracteres")]
    public void Details_ShouldBeNullableMax2000()
    {
        var p = Entity().FindProperty("Details")!;

        p.IsNullable.Should().BeTrue();
        p.GetMaxLength().Should().Be(2000);
    }

    [Fact(DisplayName = "IpAddress deve ser nullable com até 45 caracteres")]
    public void IpAddress_ShouldBeNullableMax45()
    {
        var p = Entity().FindProperty("IpAddress")!;

        p.IsNullable.Should().BeTrue();
        p.GetMaxLength().Should().Be(45);
    }

    [Fact(DisplayName = "FKs de ator e alvo para User devem usar DeleteBehavior.Restrict")]
    public void ForeignKeys_ShouldBeRestrict()
    {
        var fks = Entity().GetForeignKeys().Where(f => f.PrincipalEntityType.ClrType == typeof(User)).ToList();

        fks.Select(f => f.Properties.Single().Name).Should().BeEquivalentTo("ActorUserId", "TargetUserId");
        fks.Should().OnlyContain(f => f.DeleteBehavior == DeleteBehavior.Restrict);
    }

    [Fact(DisplayName = "Deve ter índice em CreatedAt (purge por retenção)")]
    public void Index_CreatedAt_ShouldExist()
    {
        Entity().GetIndexes().Should().Contain(i =>
            i.Properties.Select(p => p.Name).SequenceEqual(new[] { "CreatedAt" }));
    }

    [Fact(DisplayName = "Insert-only: sem DeletedAt e sem HasQueryFilter")]
    public void Entity_ShouldNotUseSoftDelete()
    {
        var e = Entity();

        e.FindProperty("DeletedAt").Should().BeNull();
        e.GetQueryFilter().Should().BeNull();
    }
}
