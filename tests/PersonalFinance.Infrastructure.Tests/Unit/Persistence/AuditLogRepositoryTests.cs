using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Domain.Entities.Auth;
using PersonalFinance.Domain.Enums;
using PersonalFinance.Infrastructure.Persistence.Context;
using PersonalFinance.Infrastructure.Persistence.Repositories.Auth;
using Xunit;

namespace PersonalFinance.Infrastructure.Tests.Unit.Persistence;

/// <summary>
/// AuditLogRepository (#402): AddAsync só marca o contexto (persistência no UnitOfWork);
/// RemoveOlderThanAsync remove em lote por consulta + RemoveRange (InMemory não suporta ExecuteDelete)
/// e retorna a quantidade marcada. Os testes persistem com SaveChangesAsync.
/// </summary>
public class AuditLogRepositoryTests
{
    private static readonly DateTime Now = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
    private readonly string _dbName = $"AuditDb_{Guid.NewGuid()}";

    private AppDbContext NewContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(_dbName).Options);

    private static AuditLog Log(DateTime at) =>
        AuditLog.Create(Guid.NewGuid(), AuditAction.UserUpdated, Guid.NewGuid(), null, "203.0.113.7", at);

    private async Task SeedAsync(params AuditLog[] rows)
    {
        await using var ctx = NewContext();
        ctx.Set<AuditLog>().AddRange(rows);
        await ctx.SaveChangesAsync();
    }

    private async Task<List<AuditLog>> AllAsync()
    {
        await using var ctx = NewContext();
        return await ctx.Set<AuditLog>().AsNoTracking().ToListAsync();
    }

    [Fact(DisplayName = "AddAsync não persiste até o SaveChanges e depois grava a linha")]
    public async Task AddAsync_ShouldPersistOnSave()
    {
        var log = Log(Now);
        await using (var ctx = NewContext())
        {
            var repo = new AuditLogRepository(ctx);
            await repo.AddAsync(log);
            (await AllAsync()).Should().BeEmpty();
            await ctx.SaveChangesAsync();
        }

        var all = await AllAsync();
        all.Should().ContainSingle().Which.Id.Should().Be(log.Id);
    }

    [Fact(DisplayName = "RemoveOlderThanAsync deve remover só linhas anteriores ao corte")]
    public async Task RemoveOlderThan_ShouldRemoveOnlyOlder()
    {
        var old1 = Log(Now.AddDays(-400));
        var old2 = Log(Now.AddDays(-366));
        var recent = Log(Now.AddDays(-10));
        await SeedAsync(old1, old2, recent);

        int removed;
        await using (var ctx = NewContext())
        {
            removed = await new AuditLogRepository(ctx).RemoveOlderThanAsync(Now.AddDays(-365), 100);
            await ctx.SaveChangesAsync();
        }

        removed.Should().Be(2);
        (await AllAsync()).Select(r => r.Id).Should().Equal(recent.Id);
    }

    [Fact(DisplayName = "RemoveOlderThanAsync deve respeitar o BatchSize, removendo as mais antigas primeiro")]
    public async Task RemoveOlderThan_ShouldRespectBatchSizeOldestFirst()
    {
        var rows = Enumerable.Range(1, 5).Select(i => Log(Now.AddDays(-400 - i))).ToArray(); // i maior = mais antiga
        await SeedAsync(rows);

        int removed;
        await using (var ctx = NewContext())
        {
            removed = await new AuditLogRepository(ctx).RemoveOlderThanAsync(Now.AddDays(-365), 3);
            await ctx.SaveChangesAsync();
        }

        removed.Should().Be(3);
        var remaining = await AllAsync();
        remaining.Should().HaveCount(2);
        remaining.Select(r => r.Id).Should().BeEquivalentTo(new[] { rows[0].Id, rows[1].Id });
    }

    [Fact(DisplayName = "RemoveOlderThanAsync sem linhas elegíveis retorna 0 e preserva tudo")]
    public async Task RemoveOlderThan_NothingEligible_ShouldReturnZero()
    {
        await SeedAsync(Log(Now.AddDays(-1)), Log(Now));

        int removed;
        await using (var ctx = NewContext())
        {
            removed = await new AuditLogRepository(ctx).RemoveOlderThanAsync(Now.AddDays(-365), 100);
            await ctx.SaveChangesAsync();
        }

        removed.Should().Be(0);
        (await AllAsync()).Should().HaveCount(2);
    }

    [Fact(DisplayName = "Linha exatamente no corte não é removida (estritamente anterior)")]
    public async Task RemoveOlderThan_RowAtCutoff_ShouldBeKept()
    {
        var cutoff = Now.AddDays(-365);
        await SeedAsync(Log(cutoff));

        await using (var ctx = NewContext())
        {
            (await new AuditLogRepository(ctx).RemoveOlderThanAsync(cutoff, 100)).Should().Be(0);
            await ctx.SaveChangesAsync();
        }

        (await AllAsync()).Should().HaveCount(1);
    }
}
