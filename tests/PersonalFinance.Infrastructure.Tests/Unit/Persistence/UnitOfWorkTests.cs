using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Domain.Entities.Auth;
using PersonalFinance.Domain.Exceptions;
using PersonalFinance.Infrastructure.Persistence;
using PersonalFinance.Infrastructure.Persistence.Context;
using Xunit;

namespace PersonalFinance.Infrastructure.Tests.Unit.Persistence;

/// <summary>
/// UnitOfWork deve traduzir DbUpdateConcurrencyException (EF) para
/// ConcurrencyConflictException (Domain), sem vazar Infrastructure para Application (#391, D2).
/// </summary>
public class UnitOfWorkTests
{
    private static AppDbContext NewContext(string dbName) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(dbName).Options);

    [Fact(DisplayName = "CommitAsync deve lançar ConcurrencyConflictException em conflito de concorrência")]
    public async Task CommitAsync_OnConcurrencyConflict_ShouldThrowDomainConflictException()
    {
        var dbName = $"UowDb_{Guid.NewGuid()}";
        var user = User.Create("Caique", $"{Guid.NewGuid():N}@x.com", "hash");

        await using (var seed = NewContext(dbName))
        {
            seed.Users.Add(user);
            await seed.SaveChangesAsync();
        }

        await using var ctxA = NewContext(dbName);
        await using var ctxB = NewContext(dbName);
        var userA = await ctxA.Users.SingleAsync(u => u.Id == user.Id);
        var userB = await ctxB.Users.SingleAsync(u => u.Id == user.Id);

        // B remove a linha; A tenta atualizar -> EF lança DbUpdateConcurrencyException (InMemory)
        ctxB.Users.Remove(userB);
        await ctxB.SaveChangesAsync();
        userA.RegisterFailedLogin(5, TimeSpan.FromMinutes(15), DateTime.UtcNow);

        var act = () => new UnitOfWork(ctxA).CommitAsync();

        var ex = await act.Should().ThrowAsync<Exception>();
        ex.Which.GetType().FullName.Should()
            .Be("PersonalFinance.Domain.Exceptions.ConcurrencyConflictException");
    }

    [Fact(DisplayName = "CommitAsync deve limpar o ChangeTracker após conflito, para o retry reler do banco")]
    public async Task CommitAsync_OnConcurrencyConflict_ShouldClearChangeTracker()
    {
        var dbName = $"UowDb_{Guid.NewGuid()}";
        var user = User.Create("Caique", $"{Guid.NewGuid():N}@x.com", "hash");

        await using (var seed = NewContext(dbName))
        {
            seed.Users.Add(user);
            await seed.SaveChangesAsync();
        }

        await using var ctxA = NewContext(dbName);
        await using var ctxB = NewContext(dbName);
        var userA = await ctxA.Users.SingleAsync(u => u.Id == user.Id);
        var userB = await ctxB.Users.SingleAsync(u => u.Id == user.Id);

        ctxB.Users.Remove(userB);
        await ctxB.SaveChangesAsync();
        userA.RegisterFailedLogin(5, TimeSpan.FromMinutes(15), DateTime.UtcNow);
        ctxA.ChangeTracker.Entries().Should().NotBeEmpty();

        var act = () => new UnitOfWork(ctxA).CommitAsync();

        await act.Should().ThrowAsync<ConcurrencyConflictException>();
        ctxA.ChangeTracker.Entries().Should().BeEmpty();
    }
}
