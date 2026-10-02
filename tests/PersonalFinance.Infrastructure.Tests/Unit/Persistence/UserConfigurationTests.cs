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
}
