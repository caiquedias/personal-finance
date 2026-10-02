using FluentAssertions;
using PersonalFinance.Domain.Entities.Auth;
using PersonalFinance.Domain.Exceptions;
using Xunit;

namespace PersonalFinance.Domain.Tests.Unit.Auth;

/// <summary>
/// Testes da entidade LoginThrottle (lockout por par conta+IP) — #391, D6.
/// A janela de contagem e a duração do bloqueio são o mesmo TimeSpan (LockoutMinutes).
/// </summary>
public class LoginThrottleTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(15);
    private static readonly Guid UserId = Guid.NewGuid();

    private static LoginThrottle NewThrottle(DateTime? at = null) =>
        LoginThrottle.Create(UserId, "203.0.113.7", at ?? Now);

    private static LoginThrottle Locked(DateTime at)
    {
        var t = NewThrottle(at);
        for (var i = 0; i < 5; i++) t.RegisterFailure(5, Window, at);
        return t;
    }

    [Fact(DisplayName = "Create deve inicializar par com contador 0, WindowStart = now e sem bloqueio")]
    public void Create_ShouldInitializePair()
    {
        var t = NewThrottle();

        t.Id.Should().NotBeEmpty();
        t.UserId.Should().Be(UserId);
        t.IpAddress.Should().Be("203.0.113.7");
        t.FailedCount.Should().Be(0);
        t.WindowStart.Should().Be(Now);
        t.LockedUntil.Should().BeNull();
        t.IsLockedOut(Now).Should().BeFalse();
    }

    [Fact(DisplayName = "Create deve gerar Ids distintos")]
    public void Create_ShouldGenerateDistinctIds() =>
        NewThrottle().Id.Should().NotBe(NewThrottle().Id);

    [Fact(DisplayName = "Create deve rejeitar userId vazio")]
    public void Create_WithEmptyUserId_ShouldThrow()
    {
        var act = () => LoginThrottle.Create(Guid.Empty, "1.1.1.1", Now);

        act.Should().Throw<DomainException>();
    }

    [Theory(DisplayName = "Create deve rejeitar IP vazio ou acima de 45 caracteres")]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithInvalidIp_ShouldThrow(string ip)
    {
        var act = () => LoginThrottle.Create(UserId, ip, Now);

        act.Should().Throw<DomainException>();
    }

    [Fact(DisplayName = "Create deve rejeitar IP com mais de 45 caracteres")]
    public void Create_WithTooLongIp_ShouldThrow()
    {
        var act = () => LoginThrottle.Create(UserId, new string('1', 46), Now);

        act.Should().Throw<DomainException>();
    }

    [Fact(DisplayName = "RegisterFailure deve incrementar sem bloquear abaixo do limite")]
    public void RegisterFailure_BelowLimit_ShouldIncrementWithoutLocking()
    {
        var t = NewThrottle();

        t.RegisterFailure(5, Window, Now);
        t.RegisterFailure(5, Window, Now.AddMinutes(1));

        t.FailedCount.Should().Be(2);
        t.LockedUntil.Should().BeNull();
        t.IsLockedOut(Now.AddMinutes(1)).Should().BeFalse();
    }

    [Fact(DisplayName = "RegisterFailure deve bloquear ao atingir o limite com LockedUntil = now + duração")]
    public void RegisterFailure_AtLimit_ShouldLockUntilNowPlusWindow()
    {
        var t = Locked(Now);

        t.LockedUntil.Should().Be(Now + Window);
        t.IsLockedOut(Now).Should().BeTrue();
    }

    [Fact(DisplayName = "IsLockedOut deve ser true dentro da janela e false a partir de LockedUntil")]
    public void IsLockedOut_ShouldDependOnLockedUntil()
    {
        var t = Locked(Now);

        t.IsLockedOut(Now.AddMinutes(14)).Should().BeTrue();
        t.IsLockedOut(Now.AddMinutes(15)).Should().BeFalse();
        t.IsLockedOut(Now.AddMinutes(16)).Should().BeFalse();
    }

    [Fact(DisplayName = "Falhas dentro da janela (no limite exato) continuam acumulando")]
    public void RegisterFailure_AtExactWindowBoundary_ShouldKeepAccumulating()
    {
        var t = NewThrottle();
        t.RegisterFailure(5, Window, Now);

        t.RegisterFailure(5, Window, Now + Window);

        t.FailedCount.Should().Be(2);
        t.WindowStart.Should().Be(Now);
    }

    [Fact(DisplayName = "Falha após a janela expirar sem bloqueio deve reiniciar contador e WindowStart")]
    public void RegisterFailure_AfterWindowExpired_ShouldRestartWindow()
    {
        var t = NewThrottle();
        t.RegisterFailure(5, Window, Now);
        t.RegisterFailure(5, Window, Now);
        t.RegisterFailure(5, Window, Now);
        var later = Now + Window + TimeSpan.FromSeconds(1);

        t.RegisterFailure(5, Window, later);

        t.FailedCount.Should().Be(1);
        t.WindowStart.Should().Be(later);
        t.LockedUntil.Should().BeNull();
    }

    [Fact(DisplayName = "Falha durante bloqueio ativo não reinicia o contador")]
    public void RegisterFailure_WhileLockActive_ShouldNotRestart()
    {
        var t = Locked(Now);

        t.RegisterFailure(5, Window, Now.AddMinutes(10));

        t.FailedCount.Should().Be(6);
        t.IsLockedOut(Now.AddMinutes(10)).Should().BeTrue();
    }

    [Fact(DisplayName = "Após expirar o bloqueio o contador recomeça do zero e o bloqueio é removido")]
    public void RegisterFailure_AfterLockExpired_ShouldRestartCounter()
    {
        var t = Locked(Now);
        var later = Now + Window;

        t.RegisterFailure(5, Window, later);

        t.FailedCount.Should().Be(1);
        t.LockedUntil.Should().BeNull();
        t.WindowStart.Should().Be(later);
        t.IsLockedOut(later).Should().BeFalse();
    }

    [Fact(DisplayName = "Reset deve zerar contador e LockedUntil")]
    public void Reset_ShouldClearCounterAndLock()
    {
        var t = Locked(Now);

        t.Reset();

        t.FailedCount.Should().Be(0);
        t.LockedUntil.Should().BeNull();
        t.IsLockedOut(Now).Should().BeFalse();
    }

    [Fact(DisplayName = "IsExpired: false dentro da janela")]
    public void IsExpired_WithinWindow_ShouldBeFalse()
    {
        var t = NewThrottle();
        t.RegisterFailure(5, Window, Now);

        t.IsExpired(Now.AddMinutes(10), Window).Should().BeFalse();
        t.IsExpired(Now + Window, Window).Should().BeFalse();
    }

    [Fact(DisplayName = "IsExpired: true após a janela sem bloqueio")]
    public void IsExpired_AfterWindowWithoutLock_ShouldBeTrue()
    {
        var t = NewThrottle();
        t.RegisterFailure(5, Window, Now);

        t.IsExpired(Now + Window + TimeSpan.FromSeconds(1), Window).Should().BeTrue();
    }

    [Fact(DisplayName = "IsExpired: false enquanto o bloqueio estiver ativo; true quando expira")]
    public void IsExpired_WithActiveLock_ShouldBeFalseUntilLockExpires()
    {
        var t = Locked(Now);

        t.IsExpired(Now.AddMinutes(14), Window).Should().BeFalse();
        t.IsExpired(Now.AddMinutes(16), Window).Should().BeTrue();
    }
}
