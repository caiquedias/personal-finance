using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using PersonalFinance.Domain.Entities.Auth;
using PersonalFinance.Infrastructure.Persistence.Context;
using Xunit;

namespace PersonalFinance.Infrastructure.Tests.Unit.Persistence;

/// <summary>
/// Mapeamento de concorrência otimista do User (rowversion) — #391, D2.
/// </summary>
public class UserConfigurationTests
{
    [Fact(DisplayName = "User deve ter RowVersion byte[] mapeado como rowversion (token de concorrência)")]
    public void User_RowVersion_ShouldBeMappedAsRowVersion()
    {
        using var ctx = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"CfgDb_{Guid.NewGuid()}").Options);

        var property = ctx.Model.FindEntityType(typeof(User))!.FindProperty("RowVersion");

        property.Should().NotBeNull("User precisa de uma propriedade RowVersion");
        property!.ClrType.Should().Be(typeof(byte[]));
        property.IsConcurrencyToken.Should().BeTrue();
        property.ValueGenerated.Should().Be(ValueGenerated.OnAddOrUpdate);
    }

    // ── MFA (#393) ────────────────────────────────────────────────────────────

    private static IEntityType UserEntity()
    {
        var ctx = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"CfgDb_{Guid.NewGuid()}").Options);
        return ctx.Model.FindEntityType(typeof(User))!;
    }

    [Fact(DisplayName = "User deve mapear as colunas de MFA com a nulabilidade correta")]
    public void User_MfaColumns_ShouldBeMappedWithExpectedNullability()
    {
        var e = UserEntity();

        e.FindProperty("MfaEnabled")!.IsNullable.Should().BeFalse();
        e.FindProperty("MfaEnabled")!.ClrType.Should().Be(typeof(bool));
        e.FindProperty("MfaSecretEncrypted")!.IsNullable.Should().BeTrue();
        e.FindProperty("MfaEnabledAt")!.IsNullable.Should().BeTrue();
        e.FindProperty("LastUsedTotpStep")!.IsNullable.Should().BeTrue();
        e.FindProperty("LastUsedTotpStep")!.ClrType.Should().Be(typeof(long?));
    }

    [Fact(DisplayName = "MfaEnabled deve ter default false no banco (usuários existentes não mudam)")]
    public void User_MfaEnabled_ShouldDefaultToFalse()
    {
        UserEntity().FindProperty("MfaEnabled")!.GetDefaultValue().Should().Be(false);
    }

    [Fact(DisplayName = "MfaSecretEncrypted deve ter tamanho máximo definido (blob cifrado, não o secret em claro)")]
    public void User_MfaSecretEncrypted_ShouldHaveMaxLength()
    {
        var maxLength = UserEntity().FindProperty("MfaSecretEncrypted")!.GetMaxLength();

        maxLength.Should().NotBeNull();
        maxLength!.Value.Should().BeGreaterOrEqualTo(128);
    }
}
