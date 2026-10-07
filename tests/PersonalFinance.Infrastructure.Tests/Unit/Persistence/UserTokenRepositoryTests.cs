using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Domain.Entities.Auth;
using PersonalFinance.Domain.Enums;
using PersonalFinance.Infrastructure.Persistence.Context;
using PersonalFinance.Infrastructure.Persistence.Repositories.Auth;
using Xunit;

namespace PersonalFinance.Infrastructure.Tests.Unit.Persistence;

/// <summary>
/// UserTokenRepository (#404). Escritas só marcam o contexto — os testes persistem com SaveChangesAsync.
/// Exclusão FÍSICA dos tokens anteriores do par (usuário, propósito).
/// </summary>
public class UserTokenRepositoryTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);

    private readonly string _dbName = $"UserTokenDb_{Guid.NewGuid()}";

    private AppDbContext NewContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(_dbName).Options);

    private static UserToken Token(Guid userId, UserTokenPurpose purpose, DateTime createdAt, string hash = "h")
    {
        var token = UserToken.Create(userId, purpose, createdAt, Ttl);
        token.SetTokenHash(hash);
        return token;
    }

    private async Task SeedAsync(params UserToken[] rows)
    {
        await using var ctx = NewContext();
        ctx.Set<UserToken>().AddRange(rows);
        await ctx.SaveChangesAsync();
    }

    [Fact(DisplayName = "AddAsync deve persistir o token")]
    public async Task AddAsync_ShouldPersist()
    {
        var userId = Guid.NewGuid();
        var token = Token(userId, UserTokenPurpose.PasswordReset, Now, "stored");
        await using (var ctx = NewContext())
        {
            await new UserTokenRepository(ctx).AddAsync(token);
            await ctx.SaveChangesAsync();
        }

        await using var read = NewContext();
        var saved = await read.Set<UserToken>().SingleAsync(t => t.Id == token.Id);
        saved.UserId.Should().Be(userId);
        saved.Purpose.Should().Be(UserTokenPurpose.PasswordReset);
        saved.TokenHash.Should().Be("stored");
        saved.ExpiresAt.Should().Be(Now + Ttl);
    }

    [Fact(DisplayName = "GetLatestAsync deve retornar o token mais recente do par (usuário, propósito)")]
    public async Task GetLatestAsync_ShouldReturnMostRecentOfPair()
    {
        var userId = Guid.NewGuid();
        var older = Token(userId, UserTokenPurpose.PasswordReset, Now.AddMinutes(-5), "older");
        var newer = Token(userId, UserTokenPurpose.PasswordReset, Now, "newer");
        var otherPurpose = Token(userId, UserTokenPurpose.EmailVerification, Now.AddMinutes(1), "other-purpose");
        var otherUser = Token(Guid.NewGuid(), UserTokenPurpose.PasswordReset, Now.AddMinutes(2), "other-user");
        await SeedAsync(older, newer, otherPurpose, otherUser);
        await using var ctx = NewContext();

        var result = await new UserTokenRepository(ctx).GetLatestAsync(userId, UserTokenPurpose.PasswordReset);

        result.Should().NotBeNull();
        result!.TokenHash.Should().Be("newer");
    }

    [Fact(DisplayName = "GetLatestAsync retorna null quando não há token do par")]
    public async Task GetLatestAsync_WithoutToken_ShouldReturnNull()
    {
        var userId = Guid.NewGuid();
        await SeedAsync(Token(userId, UserTokenPurpose.EmailVerification, Now));
        await using var ctx = NewContext();

        (await new UserTokenRepository(ctx).GetLatestAsync(userId, UserTokenPurpose.PasswordReset)).Should().BeNull();
    }

    [Fact(DisplayName = "GetLatestAsync também retorna token já usado (o use case decide via IsUsable)")]
    public async Task GetLatestAsync_ShouldReturnUsedToken()
    {
        var userId = Guid.NewGuid();
        var used = Token(userId, UserTokenPurpose.PasswordReset, Now, "used");
        used.MarkUsed(Now.AddMinutes(1));
        await SeedAsync(used);
        await using var ctx = NewContext();

        var result = await new UserTokenRepository(ctx).GetLatestAsync(userId, UserTokenPurpose.PasswordReset);

        result.Should().NotBeNull();
        result!.UsedAt.Should().NotBeNull();
    }

    [Fact(DisplayName = "RemoveAllAsync deve apagar FISICAMENTE só os tokens do par (usuário, propósito)")]
    public async Task RemoveAllAsync_ShouldHardDeleteOnlyThePair()
    {
        var userId = Guid.NewGuid();
        var used = Token(userId, UserTokenPurpose.PasswordReset, Now, "used");
        used.MarkUsed(Now.AddMinutes(1));
        var keepPurpose = Token(userId, UserTokenPurpose.EmailVerification, Now, "keep-purpose");
        var keepUser = Token(Guid.NewGuid(), UserTokenPurpose.PasswordReset, Now, "keep-user");
        await SeedAsync(Token(userId, UserTokenPurpose.PasswordReset, Now, "a"), used, keepPurpose, keepUser);

        await using (var ctx = NewContext())
        {
            await new UserTokenRepository(ctx).RemoveAllAsync(userId, UserTokenPurpose.PasswordReset);
            await ctx.SaveChangesAsync();
        }

        await using var read = NewContext();
        var remaining = await read.Set<UserToken>().IgnoreQueryFilters().ToListAsync();
        remaining.Select(t => t.TokenHash).Should().BeEquivalentTo(new[] { "keep-purpose", "keep-user" });
    }

    [Fact(DisplayName = "RemoveAllAsync sem tokens não deve falhar")]
    public async Task RemoveAllAsync_WithoutTokens_ShouldNotThrow()
    {
        await using var ctx = NewContext();

        var act = async () =>
        {
            await new UserTokenRepository(ctx).RemoveAllAsync(Guid.NewGuid(), UserTokenPurpose.PasswordReset);
            await ctx.SaveChangesAsync();
        };

        await act.Should().NotThrowAsync();
    }

    [Fact(DisplayName = "UpdateAsync deve persistir tentativas registradas")]
    public async Task UpdateAsync_ShouldPersistAttempts()
    {
        var userId = Guid.NewGuid();
        var token = Token(userId, UserTokenPurpose.PasswordReset, Now);
        await SeedAsync(token);

        await using (var ctx = NewContext())
        {
            var repo = new UserTokenRepository(ctx);
            var loaded = (await repo.GetLatestAsync(userId, UserTokenPurpose.PasswordReset))!;
            loaded.RegisterFailedAttempt(3);
            loaded.RegisterFailedAttempt(3);
            await repo.UpdateAsync(loaded);
            await ctx.SaveChangesAsync();
        }

        await using var read = NewContext();
        var saved = await read.Set<UserToken>().SingleAsync(t => t.Id == token.Id);
        saved.Attempts.Should().Be(2);
        saved.IsUsable(Now.AddMinutes(1)).Should().BeTrue();
    }

    [Fact(DisplayName = "Invalidação por 3 tentativas persiste: token deixa de ser utilizável após recarregar")]
    public async Task Invalidation_ShouldSurviveReload()
    {
        var userId = Guid.NewGuid();
        var token = Token(userId, UserTokenPurpose.PasswordReset, Now);
        await SeedAsync(token);

        await using (var ctx = NewContext())
        {
            var repo = new UserTokenRepository(ctx);
            var loaded = (await repo.GetLatestAsync(userId, UserTokenPurpose.PasswordReset))!;
            for (var i = 0; i < 3; i++) loaded.RegisterFailedAttempt(3);
            await repo.UpdateAsync(loaded);
            await ctx.SaveChangesAsync();
        }

        await using var read = NewContext();
        var saved = await read.Set<UserToken>().SingleAsync(t => t.Id == token.Id);
        saved.IsUsable(Now.AddMinutes(1)).Should().BeFalse();
    }
}
