using FluentAssertions;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using PersonalFinance.Infrastructure.Persistence.Context;
using System.Reflection;
using Xunit;

namespace PersonalFinance.Infrastructure.Tests.Unit.Persistence;

/// <summary>
/// Migration AddUserTokenAndEmailConfirmation (#404): cria a tabela UserToken, adiciona User.EmailConfirmedAt e
/// faz o BACKFILL dos usuários existentes (senão o Enforce trancaria todo mundo para fora). O comportamento
/// real só é validável em SQL Server — aqui se inspeciona o plano de operações e o snapshot do modelo.
/// </summary>
public class AddUserTokenAndEmailConfirmationMigrationTests
{
    private static readonly Assembly InfraAssembly = typeof(AppDbContext).Assembly;

    private static Migration CreateMigration()
    {
        var type = InfraAssembly.GetTypes().FirstOrDefault(t => t.Name == "AddUserTokenAndEmailConfirmation");
        type.Should().NotBeNull("a migration AddUserTokenAndEmailConfirmation deve existir");
        return (Migration)Activator.CreateInstance(type!)!;
    }

    private static List<MigrationOperation> Operations(string direction)
    {
        var migration = CreateMigration();
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        typeof(Migration).GetMethod(direction, BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(migration, new object[] { builder });
        return builder.Operations;
    }

    [Fact(DisplayName = "Up deve criar a tabela UserToken")]
    public void Up_ShouldCreateUserTokenTable()
    {
        var create = Operations("Up").OfType<CreateTableOperation>().SingleOrDefault(o => o.Name == "UserToken");

        create.Should().NotBeNull();
        create!.Columns.Select(c => c.Name).Should().Contain(
            new[] { "Id", "UserId", "Purpose", "TokenHash", "ExpiresAt", "UsedAt", "Attempts", "RowVersion" });
        create.ForeignKeys.Should().Contain(fk => fk.PrincipalTable == "User" && fk.OnDelete == ReferentialAction.Restrict);
    }

    [Fact(DisplayName = "Up deve adicionar User.EmailConfirmedAt como nullable")]
    public void Up_ShouldAddEmailConfirmedAtToUser()
    {
        var add = Operations("Up").OfType<AddColumnOperation>()
            .SingleOrDefault(o => o.Table == "User" && o.Name == "EmailConfirmedAt");

        add.Should().NotBeNull();
        add!.IsNullable.Should().BeTrue();
    }

    [Fact(DisplayName = "Up deve fazer backfill EmailConfirmedAt = SYSUTCDATETIME() para usuários existentes, APÓS criar a coluna")]
    public void Up_ShouldBackfillExistingUsersAfterAddingColumn()
    {
        var ops = Operations("Up");
        var addIndex = ops.FindIndex(o => o is AddColumnOperation { Table: "User", Name: "EmailConfirmedAt" });
        var sqlIndex = ops.FindIndex(o => o is SqlOperation s
            && s.Sql.Contains("EmailConfirmedAt", StringComparison.OrdinalIgnoreCase)
            && s.Sql.Contains("SYSUTCDATETIME()", StringComparison.OrdinalIgnoreCase)
            && s.Sql.Contains("UPDATE", StringComparison.OrdinalIgnoreCase));

        addIndex.Should().BeGreaterOrEqualTo(0);
        sqlIndex.Should().BeGreaterThan(addIndex, "o backfill precisa vir depois de criar a coluna");
    }

    [Fact(DisplayName = "Down deve remover a tabela UserToken e a coluna EmailConfirmedAt")]
    public void Down_ShouldDropTableAndColumn()
    {
        var ops = Operations("Down");

        ops.OfType<DropTableOperation>().Should().Contain(o => o.Name == "UserToken");
        ops.OfType<DropColumnOperation>().Should().Contain(o => o.Table == "User" && o.Name == "EmailConfirmedAt");
    }

    [Fact(DisplayName = "Snapshot do modelo deve conter UserToken e User.EmailConfirmedAt")]
    public void Snapshot_ShouldContainNewModel()
    {
        var snapshotType = InfraAssembly.GetTypes().Single(t => t.Name == "AppDbContextModelSnapshot");
        var snapshot = (ModelSnapshot)Activator.CreateInstance(snapshotType, nonPublic: true)!;

        snapshot.Model.FindEntityType("PersonalFinance.Domain.Entities.Auth.UserToken").Should().NotBeNull();
        snapshot.Model.FindEntityType("PersonalFinance.Domain.Entities.Auth.User")!
            .FindProperty("EmailConfirmedAt").Should().NotBeNull();
    }
}
