using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Domain.Entities.Auth;
using PersonalFinance.Infrastructure.Persistence.Context;
using PersonalFinance.Infrastructure.Persistence.Repositories.Auth;
using Xunit;

namespace PersonalFinance.Infrastructure.Tests.Unit.Persistence;

/// <summary>
/// LoginThrottleRepository (#391, D6): upsert por par, limpeza por tempo antes de inserir,
/// teto duro de linhas e fail-open. InMemory não suporta ExecuteDelete — a implementação usa RemoveRange.
/// Os métodos de escrita só marcam o contexto; os testes persistem com SaveChangesAsync.
/// </summary>
public class LoginThrottleRepositoryTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(15);
    private const int MaxRows = 2000;
    private const int Batch = 500;

    private readonly string _dbName = $"ThrottleDb_{Guid.NewGuid()}";

    private AppDbContext NewContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(_dbName).Options);

    private static string Ip(int n) => $"10.0.{n / 250}.{n % 250}";

    /// <summary>Linha cuja janela começou em <paramref name="startedAt"/> (sem bloqueio).</summary>
    private static LoginThrottle Plain(int n, DateTime startedAt)
    {
        var t = LoginThrottle.Create(Guid.NewGuid(), Ip(n), startedAt);
        t.RegisterFailure(5, Window, startedAt);
        return t;
    }

    /// <summary>Linha com bloqueio ativo em <see cref="Now"/> (5 falhas há 5 minutos).</summary>
    private static LoginThrottle LockedNow(int n)
    {
        var at = Now.AddMinutes(-5);
        var t = LoginThrottle.Create(Guid.NewGuid(), Ip(n), at);
        for (var i = 0; i < 5; i++) t.RegisterFailure(5, Window, at);
        return t;
    }

    private async Task SeedAsync(params LoginThrottle[] rows)
    {
        await using var ctx = NewContext();
        ctx.Set<LoginThrottle>().AddRange(rows);
        await ctx.SaveChangesAsync();
    }

    private async Task<List<LoginThrottle>> AllAsync()
    {
        await using var ctx = NewContext();
        return await ctx.Set<LoginThrottle>().AsNoTracking().ToListAsync();
    }

    private async Task<bool> TryAddAndSaveAsync(LoginThrottle row, int maxRows = MaxRows, int batch = Batch)
    {
        await using var ctx = NewContext();
        var repo = new LoginThrottleRepository(ctx);
        var added = await repo.TryAddAsync(row, Now, Window, maxRows, batch);
        await ctx.SaveChangesAsync();
        return added;
    }

    [Fact(DisplayName = "GetAsync deve retornar a linha do par (userId, ip) e null para outro IP")]
    public async Task GetAsync_ShouldReturnRowByPair()
    {
        var row = Plain(1, Now);
        await SeedAsync(row);
        await using var ctx = NewContext();
        var repo = new LoginThrottleRepository(ctx);

        var found = await repo.GetAsync(row.UserId, row.IpAddress);
        var otherIp = await repo.GetAsync(row.UserId, "198.51.100.1");
        var otherUser = await repo.GetAsync(Guid.NewGuid(), row.IpAddress);

        found.Should().NotBeNull();
        found!.Id.Should().Be(row.Id);
        otherIp.Should().BeNull();
        otherUser.Should().BeNull();
    }

    [Fact(DisplayName = "TryAddAsync em tabela vazia deve inserir o par e retornar true")]
    public async Task TryAddAsync_OnEmptyTable_ShouldInsert()
    {
        var row = Plain(1, Now);

        var added = await TryAddAndSaveAsync(row);

        added.Should().BeTrue();
        (await AllAsync()).Should().ContainSingle(r => r.Id == row.Id);
    }

    [Fact(DisplayName = "TryAddAsync deve apagar linhas expiradas antes de inserir, preservando ativas e bloqueadas")]
    public async Task TryAddAsync_ShouldCleanExpiredRowsKeepingActiveAndLocked()
    {
        var expired = new[] { Plain(1, Now.AddMinutes(-30)), Plain(2, Now.AddMinutes(-40)), Plain(3, Now.AddMinutes(-16)) };
        var active = Plain(4, Now.AddMinutes(-1));
        var locked = LockedNow(5);
        await SeedAsync(expired.Append(active).Append(locked).ToArray());
        var fresh = Plain(6, Now);

        var added = await TryAddAndSaveAsync(fresh);

        added.Should().BeTrue();
        var ids = (await AllAsync()).Select(r => r.Id).ToHashSet();
        ids.Should().BeEquivalentTo(new[] { active.Id, locked.Id, fresh.Id });
    }

    [Fact(DisplayName = "Limpeza por tempo respeita o tamanho do lote")]
    public async Task TryAddAsync_ShouldRespectCleanupBatchSize()
    {
        var expired = Enumerable.Range(1, 5).Select(i => Plain(i, Now.AddMinutes(-30 - i))).ToArray();
        await SeedAsync(expired);
        var fresh = Plain(10, Now);

        var added = await TryAddAndSaveAsync(fresh, batch: 2);

        added.Should().BeTrue();
        var rows = await AllAsync();
        rows.Should().HaveCount(3 + 1); // 5 expiradas - lote de 2 removidas + 1 nova
        rows.Should().Contain(r => r.Id == fresh.Id);
    }

    [Fact(DisplayName = "Teto duro: no limite, apaga as mais antigas NÃO bloqueadas e insere o novo par")]
    public async Task TryAddAsync_AtCeiling_ShouldEvictOldestUnblockedRows()
    {
        var oldest = Plain(1, Now.AddMinutes(-9));
        var middle = Plain(2, Now.AddMinutes(-5));
        var newest = Plain(3, Now.AddMinutes(-2));
        await SeedAsync(oldest, middle, newest);
        var fresh = Plain(4, Now);

        var added = await TryAddAndSaveAsync(fresh, maxRows: 3);

        added.Should().BeTrue();
        var ids = (await AllAsync()).Select(r => r.Id).ToHashSet();
        ids.Should().HaveCount(3);
        ids.Should().NotContain(oldest.Id);
        ids.Should().Contain(new[] { middle.Id, newest.Id, fresh.Id });
    }

    [Fact(DisplayName = "Teto duro: linhas bloqueadas nunca são removidas, só as não bloqueadas")]
    public async Task TryAddAsync_AtCeiling_ShouldNeverEvictLockedRows()
    {
        var lockedA = LockedNow(1);
        var lockedB = LockedNow(2);
        var plain = Plain(3, Now.AddMinutes(-2));
        await SeedAsync(lockedA, lockedB, plain);
        var fresh = Plain(4, Now);

        var added = await TryAddAndSaveAsync(fresh, maxRows: 3);

        added.Should().BeTrue();
        var ids = (await AllAsync()).Select(r => r.Id).ToHashSet();
        ids.Should().BeEquivalentTo(new[] { lockedA.Id, lockedB.Id, fresh.Id });
    }

    [Fact(DisplayName = "Teto duro com tudo bloqueado: NÃO insere o par (fail-open) e não apaga nada")]
    public async Task TryAddAsync_AllLockedAtCeiling_ShouldReturnFalseWithoutInserting()
    {
        var lockedA = LockedNow(1);
        var lockedB = LockedNow(2);
        await SeedAsync(lockedA, lockedB);
        var fresh = Plain(3, Now);

        var added = await TryAddAndSaveAsync(fresh, maxRows: 2);

        added.Should().BeFalse();
        var ids = (await AllAsync()).Select(r => r.Id).ToHashSet();
        ids.Should().BeEquivalentTo(new[] { lockedA.Id, lockedB.Id });
    }

    [Fact(DisplayName = "UpdateAsync deve persistir a alteração do par")]
    public async Task UpdateAsync_ShouldPersistChanges()
    {
        var row = Plain(1, Now);
        await SeedAsync(row);

        await using (var ctx = NewContext())
        {
            var repo = new LoginThrottleRepository(ctx);
            var tracked = (await repo.GetAsync(row.UserId, row.IpAddress))!;
            tracked.RegisterFailure(5, Window, Now.AddMinutes(1));
            await repo.UpdateAsync(tracked);
            await ctx.SaveChangesAsync();
        }

        (await AllAsync()).Single().FailedCount.Should().Be(2);
    }

    [Fact(DisplayName = "RemoveAsync deve excluir fisicamente a linha (sem soft-delete)")]
    public async Task RemoveAsync_ShouldDeletePhysically()
    {
        var row = Plain(1, Now);
        var keep = Plain(2, Now);
        await SeedAsync(row, keep);

        await using (var ctx = NewContext())
        {
            var repo = new LoginThrottleRepository(ctx);
            var tracked = (await repo.GetAsync(row.UserId, row.IpAddress))!;
            await repo.RemoveAsync(tracked);
            await ctx.SaveChangesAsync();
        }

        var rows = await AllAsync();
        rows.Should().ContainSingle(r => r.Id == keep.Id);
    }

    [Fact(DisplayName = "CountAsync deve retornar o total de linhas")]
    public async Task CountAsync_ShouldReturnTotalRows()
    {
        await SeedAsync(Plain(1, Now), Plain(2, Now), LockedNow(3));
        await using var ctx = NewContext();
        var repo = new LoginThrottleRepository(ctx);

        (await repo.CountAsync()).Should().Be(3);
    }
}
