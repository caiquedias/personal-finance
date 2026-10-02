using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using PersonalFinance.Domain.Entities.Auth;
using PersonalFinance.Infrastructure.Persistence.Context;
using Xunit;

namespace PersonalFinance.Infrastructure.Tests.Unit.Persistence;

/// <summary>
/// Mapeamento EF de LoginThrottle (#391, D6): rowversion, índice único (UserId, IpAddress),
/// FK Restrict e exceção deliberada ao soft-delete (tabela efêmera, exclusão física).
/// </summary>
public class LoginThrottleConfigurationTests
{
    private static IEntityType Entity()
    {
        var ctx = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ThrottleCfg_{Guid.NewGuid()}").Options);
        return ctx.Model.FindEntityType(typeof(LoginThrottle))!;
    }

    [Fact(DisplayName = "LoginThrottle deve estar mapeada no modelo")]
    public void Entity_ShouldBeMapped() => Entity().Should().NotBeNull();

    [Fact(DisplayName = "RowVersion deve ser rowversion (token de concorrência)")]
    public void RowVersion_ShouldBeConcurrencyToken()
    {
        var p = Entity().FindProperty("RowVersion");

        p.Should().NotBeNull();
        p!.ClrType.Should().Be(typeof(byte[]));
        p.IsConcurrencyToken.Should().BeTrue();
        p.ValueGenerated.Should().Be(ValueGenerated.OnAddOrUpdate);
    }

    [Fact(DisplayName = "Deve ter índice ÚNICO em (UserId, IpAddress)")]
    public void Index_UserIdAndIp_ShouldBeUnique()
    {
        var index = Entity().GetIndexes().SingleOrDefault(i =>
            i.Properties.Select(p => p.Name).SequenceEqual(new[] { "UserId", "IpAddress" }));

        index.Should().NotBeNull();
        index!.IsUnique.Should().BeTrue();
    }

    [Fact(DisplayName = "IpAddress deve ser obrigatório com até 45 caracteres")]
    public void IpAddress_ShouldBeRequiredMax45()
    {
        var p = Entity().FindProperty("IpAddress")!;

        p.IsNullable.Should().BeFalse();
        p.GetMaxLength().Should().Be(45);
    }

    [Fact(DisplayName = "LockedUntil deve ser nullable e FailedCount/WindowStart obrigatórios")]
    public void Columns_ShouldHaveExpectedNullability()
    {
        var e = Entity();

        e.FindProperty("LockedUntil")!.IsNullable.Should().BeTrue();
        e.FindProperty("FailedCount")!.IsNullable.Should().BeFalse();
        e.FindProperty("WindowStart")!.IsNullable.Should().BeFalse();
    }

    [Fact(DisplayName = "FK para User deve usar DeleteBehavior.Restrict")]
    public void ForeignKeyToUser_ShouldBeRestrict()
    {
        var fk = Entity().GetForeignKeys().Single(f => f.PrincipalEntityType.ClrType == typeof(User));

        fk.DeleteBehavior.Should().Be(DeleteBehavior.Restrict);
        fk.Properties.Single().Name.Should().Be("UserId");
    }

    [Fact(DisplayName = "Exceção ao soft-delete: sem DeletedAt e sem HasQueryFilter (exclusão física)")]
    public void Entity_ShouldNotUseSoftDelete()
    {
        var e = Entity();

        e.FindProperty("DeletedAt").Should().BeNull();
        e.GetQueryFilter().Should().BeNull();
    }
}
