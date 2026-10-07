using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Domain.Entities.Auth;
using PersonalFinance.Infrastructure.Persistence.Context;
using PersonalFinance.Infrastructure.Persistence.Repositories.Auth;
using Xunit;

namespace PersonalFinance.Infrastructure.Tests.Unit.Persistence;

/// <summary>
/// MfaRecoveryCodeRepository (#393). Escritas só marcam o contexto — os testes persistem com SaveChangesAsync.
/// </summary>
public class MfaRecoveryCodeRepositoryTests
{
    private readonly string _dbName = $"MfaCodeDb_{Guid.NewGuid()}";

    private AppDbContext NewContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(_dbName).Options);

    private async Task SeedAsync(params MfaRecoveryCode[] rows)
    {
        await using var ctx = NewContext();
        ctx.Set<MfaRecoveryCode>().AddRange(rows);
        await ctx.SaveChangesAsync();
    }

    [Fact(DisplayName = "AddRangeAsync deve persistir todos os códigos do usuário")]
    public async Task AddRangeAsync_ShouldPersistAll()
    {
        var userId = Guid.NewGuid();
        await using (var ctx = NewContext())
        {
            var repo = new MfaRecoveryCodeRepository(ctx);
            await repo.AddRangeAsync(new[]
            {
                MfaRecoveryCode.Create(userId, "h1"), MfaRecoveryCode.Create(userId, "h2"), MfaRecoveryCode.Create(userId, "h3")
            });
            await ctx.SaveChangesAsync();
        }

        await using var read = NewContext();
        (await read.Set<MfaRecoveryCode>().CountAsync(c => c.UserId == userId)).Should().Be(3);
    }

    [Fact(DisplayName = "GetActiveByUserIdAsync deve retornar só códigos do usuário, não usados e não deletados")]
    public async Task GetActiveByUserIdAsync_ShouldReturnOnlyUnusedUndeletedOfUser()
    {
        var userId = Guid.NewGuid();
        var active = MfaRecoveryCode.Create(userId, "active");
        var used = MfaRecoveryCode.Create(userId, "used");
        used.MarkUsed(DateTime.UtcNow);
        var deleted = MfaRecoveryCode.Create(userId, "deleted");
        deleted.SoftDelete();
        var other = MfaRecoveryCode.Create(Guid.NewGuid(), "other-user");
        await SeedAsync(active, used, deleted, other);
        await using var ctx = NewContext();
        var repo = new MfaRecoveryCodeRepository(ctx);

        var result = await repo.GetActiveByUserIdAsync(userId);

        result.Select(c => c.CodeHash).Should().BeEquivalentTo(new[] { "active" });
    }

    [Fact(DisplayName = "RemoveAllByUserIdAsync deve fazer soft-delete de todos os códigos do usuário (usados ou não)")]
    public async Task RemoveAllByUserIdAsync_ShouldSoftDeleteAllOfUser()
    {
        var userId = Guid.NewGuid();
        var used = MfaRecoveryCode.Create(userId, "used");
        used.MarkUsed(DateTime.UtcNow);
        var other = MfaRecoveryCode.Create(Guid.NewGuid(), "other-user");
        await SeedAsync(MfaRecoveryCode.Create(userId, "a"), MfaRecoveryCode.Create(userId, "b"), used, other);

        await using (var ctx = NewContext())
        {
            var repo = new MfaRecoveryCodeRepository(ctx);
            await repo.RemoveAllByUserIdAsync(userId);
            await ctx.SaveChangesAsync();
        }

        await using var read = NewContext();
        var mine = await read.Set<MfaRecoveryCode>().IgnoreQueryFilters().Where(c => c.UserId == userId).ToListAsync();
        mine.Should().HaveCount(3).And.OnlyContain(c => c.DeletedAt != null);
        (await new MfaRecoveryCodeRepository(read).GetActiveByUserIdAsync(userId)).Should().BeEmpty();
        var others = await read.Set<MfaRecoveryCode>().Where(c => c.UserId == other.UserId).ToListAsync();
        others.Should().ContainSingle().Which.DeletedAt.Should().BeNull();
    }

    [Fact(DisplayName = "MarkUsed persistido deve tirar o código da lista de ativos")]
    public async Task UsedCode_ShouldNotBeActiveAnymore()
    {
        var userId = Guid.NewGuid();
        var code = MfaRecoveryCode.Create(userId, "h1");
        await SeedAsync(code);

        await using (var ctx = NewContext())
        {
            var repo = new MfaRecoveryCodeRepository(ctx);
            var loaded = (await repo.GetActiveByUserIdAsync(userId)).Single();
            loaded.MarkUsed(DateTime.UtcNow);
            await ctx.SaveChangesAsync();
        }

        await using var read = NewContext();
        (await new MfaRecoveryCodeRepository(read).GetActiveByUserIdAsync(userId)).Should().BeEmpty();
    }
}
