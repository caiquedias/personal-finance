using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using PersonalFinance.Domain.Entities.Auth;
using PersonalFinance.Domain.Enums;
using PersonalFinance.Infrastructure.Persistence.Context;
using Xunit;

namespace PersonalFinance.Infrastructure.Tests.Unit.Persistence;

/// <summary>
/// Mapeamento EF de UserToken (#404): PK do Domain, FK Restrict, enum como int, rowversion,
/// índice (UserId, Purpose) e exceção deliberada ao soft-delete (hard delete dos anteriores).
/// </summary>
public class UserTokenConfigurationTests
{
    private static IEntityType Entity()
    {
        var ctx = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"UserTokenCfg_{Guid.NewGuid()}").Options);
        return ctx.Model.FindEntityType(typeof(UserToken))!;
    }

    [Fact(DisplayName = "UserToken deve estar mapeada no modelo")]
    public void Entity_ShouldBeMapped() => Entity().Should().NotBeNull();

    [Fact(DisplayName = "PK Id deve ser ValueGeneratedNever (Guid gerado no Domain)")]
    public void PrimaryKey_ShouldBeValueGeneratedNever()
    {
        var pk = Entity().FindPrimaryKey()!;

        pk.Properties.Should().ContainSingle().Which.Name.Should().Be("Id");
        pk.Properties[0].ValueGenerated.Should().Be(ValueGenerated.Never);
    }

    [Fact(DisplayName = "TokenHash obrigatório com tamanho máximo; UsedAt nullable; Attempts obrigatório")]
    public void Columns_ShouldHaveExpectedNullability()
    {
        var e = Entity();

        e.FindProperty("TokenHash")!.IsNullable.Should().BeFalse();
        e.FindProperty("TokenHash")!.GetMaxLength().Should().NotBeNull();
        e.FindProperty("UserId")!.IsNullable.Should().BeFalse();
        e.FindProperty("ExpiresAt")!.IsNullable.Should().BeFalse();
        e.FindProperty("CreatedAt")!.IsNullable.Should().BeFalse();
        e.FindProperty("UsedAt")!.IsNullable.Should().BeTrue();
        e.FindProperty("Attempts")!.IsNullable.Should().BeFalse();
    }

    [Fact(DisplayName = "Purpose deve ser persistido como int (HasConversion<int>)")]
    public void Purpose_ShouldBeStoredAsInt()
    {
        var p = Entity().FindProperty("Purpose")!;

        p.IsNullable.Should().BeFalse();
        p.ClrType.Should().Be(typeof(UserTokenPurpose));
        (p.GetValueConverter()?.ProviderClrType ?? p.GetProviderClrType() ?? p.ClrType).Should().Be(typeof(int));
    }

    [Fact(DisplayName = "RowVersion deve ser rowversion (token de concorrência)")]
    public void RowVersion_ShouldBeConcurrencyToken()
    {
        var p = Entity().FindProperty("RowVersion");

        p.Should().NotBeNull();
        p!.ClrType.Should().Be(typeof(byte[]));
        p.IsConcurrencyToken.Should().BeTrue();
        p.ValueGenerated.Should().Be(ValueGenerated.OnAddOrUpdate);
    }

    [Fact(DisplayName = "FK para User deve usar DeleteBehavior.Restrict")]
    public void ForeignKeyToUser_ShouldBeRestrict()
    {
        var fk = Entity().GetForeignKeys().Single(f => f.PrincipalEntityType.ClrType == typeof(User));

        fk.DeleteBehavior.Should().Be(DeleteBehavior.Restrict);
        fk.Properties.Single().Name.Should().Be("UserId");
    }

    [Fact(DisplayName = "Deve ter índice em (UserId, Purpose)")]
    public void Index_UserIdAndPurpose_ShouldExist()
    {
        Entity().GetIndexes().Should().Contain(i =>
            i.Properties.Select(p => p.Name).SequenceEqual(new[] { "UserId", "Purpose" }));
    }

    [Fact(DisplayName = "Exceção ao soft-delete: sem DeletedAt e sem HasQueryFilter (exclusão física)")]
    public void Entity_ShouldNotUseSoftDelete()
    {
        var e = Entity();

        e.FindProperty("DeletedAt").Should().BeNull();
        e.GetQueryFilter().Should().BeNull();
    }

    [Fact(DisplayName = "Tabela deve se chamar UserToken")]
    public void Table_ShouldBeNamedUserToken() => Entity().GetTableName().Should().Be("UserToken");
}
