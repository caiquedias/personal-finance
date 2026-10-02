using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using PersonalFinance.Domain.Entities.Auth;
using PersonalFinance.Infrastructure.Persistence.Context;
using Xunit;

namespace PersonalFinance.Infrastructure.Tests.Unit.Persistence;

/// <summary>
/// Mapeamento EF de MfaRecoveryCode (#393): PK gerada no Domain, FK Restrict para User,
/// soft-delete com query filter global e índice por UserId.
/// </summary>
public class MfaRecoveryCodeConfigurationTests
{
    private static IEntityType Entity()
    {
        var ctx = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"MfaCodeCfg_{Guid.NewGuid()}").Options);
        return ctx.Model.FindEntityType(typeof(MfaRecoveryCode))!;
    }

    [Fact(DisplayName = "MfaRecoveryCode deve estar mapeada no modelo")]
    public void Entity_ShouldBeMapped() => Entity().Should().NotBeNull();

    [Fact(DisplayName = "PK Id deve ser ValueGeneratedNever (Guid gerado no Domain)")]
    public void PrimaryKey_ShouldBeValueGeneratedNever()
    {
        var pk = Entity().FindPrimaryKey()!;

        pk.Properties.Should().ContainSingle().Which.Name.Should().Be("Id");
        pk.Properties[0].ValueGenerated.Should().Be(ValueGenerated.Never);
    }

    [Fact(DisplayName = "CodeHash deve ser obrigatório e UsedAt nullable")]
    public void Columns_ShouldHaveExpectedNullability()
    {
        var e = Entity();

        e.FindProperty("CodeHash")!.IsNullable.Should().BeFalse();
        e.FindProperty("CodeHash")!.GetMaxLength().Should().NotBeNull();
        e.FindProperty("UserId")!.IsNullable.Should().BeFalse();
        e.FindProperty("UsedAt")!.IsNullable.Should().BeTrue();
    }

    [Fact(DisplayName = "FK para User deve usar DeleteBehavior.Restrict")]
    public void ForeignKeyToUser_ShouldBeRestrict()
    {
        var fk = Entity().GetForeignKeys().Single(f => f.PrincipalEntityType.ClrType == typeof(User));

        fk.DeleteBehavior.Should().Be(DeleteBehavior.Restrict);
        fk.Properties.Single().Name.Should().Be("UserId");
    }

    [Fact(DisplayName = "Deve usar soft-delete: DeletedAt mapeado e query filter global")]
    public void Entity_ShouldUseSoftDelete()
    {
        var e = Entity();

        e.FindProperty("DeletedAt").Should().NotBeNull();
        e.GetQueryFilter().Should().NotBeNull();
    }

    [Fact(DisplayName = "Deve ter índice em UserId")]
    public void Index_UserId_ShouldExist()
    {
        Entity().GetIndexes().Should().Contain(i =>
            i.Properties.Select(p => p.Name).SequenceEqual(new[] { "UserId" }));
    }
}
