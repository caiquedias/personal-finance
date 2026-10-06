using FluentAssertions;
using Moq;
using PersonalFinance.Application.DTOs.Auth;
using PersonalFinance.Application.Options;
using PersonalFinance.Application.UseCases.Admin;
using PersonalFinance.Application.UseCases.Auth;
using PersonalFinance.Domain.Entities.Auth;
using PersonalFinance.Domain.Exceptions;
using PersonalFinance.Domain.Interfaces.Repositories;
using PersonalFinance.Domain.Interfaces.Services;
using FluentValidation;
using PersonalFinance.Application.Tests.Unit.Support;
using Xunit;

namespace PersonalFinance.Application.Tests.Unit.Auth;

public class LoginWithRolesUseCaseTests
{
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<IUserRoleRepository> _roleRepo = new();
    private readonly Mock<ILoginThrottleRepository> _throttleRepo = new();
    private readonly Mock<IPasswordHasher> _hasher = new();
    private readonly Mock<ITokenService> _tokenSvc = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly LoginWithRolesUseCase _sut;
    private readonly MfaOptions _mfaOptions = new();
    private readonly EmailVerificationOptions _emailOptions = new();

    public LoginWithRolesUseCaseTests()
    {
        _sut = new LoginWithRolesUseCase(
            _userRepo.Object, _roleRepo.Object, _throttleRepo.Object,
            _hasher.Object, _tokenSvc.Object,
            _uow.Object, new LoginLockoutOptions(), _mfaOptions, _emailOptions, TestValidators.Valid<LoginDto>());
    }

    private static User FakeUser() =>
        User.Create("Caique", "caique@monkeybomb.com", "hashed_password");

    [Fact(DisplayName = "Deve retornar token com roles para credenciais válidas")]
    public async Task Execute_WithValidCredentials_ShouldReturnTokenWithRoles()
    {
        var user = FakeUser();
        var roles = new[] { "User" };

        _userRepo.Setup(r => r.GetByEmailAsync("caique@monkeybomb.com", default)).ReturnsAsync(user);
        _hasher.Setup(h => h.Verify("Senha@123", "hashed_password")).Returns(true);
        _roleRepo.Setup(r => r.GetRoleNamesByUserIdAsync(user.Id, default)).ReturnsAsync(roles);
        _tokenSvc.Setup(t => t.Generate(user, roles)).Returns("jwt_token");

        var result = await _sut.ExecuteAsync(new LoginDto("caique@monkeybomb.com", "Senha@123"), "1.1.1.1");

        result.Token.Should().Be("jwt_token");
        result.Email.Should().Be("caique@monkeybomb.com");
    }

    [Fact(DisplayName = "Deve incluir todas as roles do usuário no token")]
    public async Task Execute_AdminUser_ShouldPassAllRolesToToken()
    {
        var user = FakeUser();
        var roles = new[] { "Admin", "User" };

        _userRepo.Setup(r => r.GetByEmailAsync("caique@monkeybomb.com", default)).ReturnsAsync(user);
        _hasher.Setup(h => h.Verify("Senha@123", "hashed_password")).Returns(true);
        _roleRepo.Setup(r => r.GetRoleNamesByUserIdAsync(user.Id, default)).ReturnsAsync(roles);
        _tokenSvc.Setup(t => t.Generate(user, roles)).Returns("admin_token");

        var result = await _sut.ExecuteAsync(new LoginDto("caique@monkeybomb.com", "Senha@123"), "1.1.1.1");

        result.Token.Should().Be("admin_token");
        _tokenSvc.Verify(t => t.Generate(user, roles), Times.Once);
    }

    [Fact(DisplayName = "Deve lançar exceção para e-mail não cadastrado")]
    public async Task Execute_WithUnknownEmail_ShouldThrow()
    {
        _userRepo.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), default))
                 .ReturnsAsync((User?)null);

        var act = () => _sut.ExecuteAsync(new LoginDto("x@x.com", "Senha@123"), "1.1.1.1");

        await act.Should().ThrowAsync<DomainException>().WithMessage("*credenciais*");
    }

    [Fact(DisplayName = "Deve lançar exceção para senha incorreta")]
    public async Task Execute_WithWrongPassword_ShouldThrow()
    {
        var user = FakeUser();
        _userRepo.Setup(r => r.GetByEmailAsync("caique@monkeybomb.com", default)).ReturnsAsync(user);
        _hasher.Setup(h => h.Verify("Errada", "hashed_password")).Returns(false);

        var act = () => _sut.ExecuteAsync(new LoginDto("caique@monkeybomb.com", "Errada"), "1.1.1.1");

        await act.Should().ThrowAsync<DomainException>().WithMessage("*credenciais*");
    }

    [Fact(DisplayName = "Deve lançar exceção para usuário inativo")]
    public async Task Execute_WithInactiveUser_ShouldThrow()
    {
        var user = FakeUser();
        user.SoftDelete();
        _userRepo.Setup(r => r.GetByEmailAsync("caique@monkeybomb.com", default)).ReturnsAsync(user);

        var act = () => _sut.ExecuteAsync(new LoginDto("caique@monkeybomb.com", "Senha@123"), "1.1.1.1");

        await act.Should().ThrowAsync<DomainException>().WithMessage("Credenciais inválidas.");
    }

    [Fact(DisplayName = "Não deve gerar token se senha for inválida")]
    public async Task Execute_WithWrongPassword_ShouldNotGenerateToken()
    {
        var user = FakeUser();
        _userRepo.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), default)).ReturnsAsync(user);
        _hasher.Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>())).Returns(false);

        try { await _sut.ExecuteAsync(new LoginDto("caique@monkeybomb.com", "Errada"), "1.1.1.1"); }
        catch { /* esperado */ }

        _tokenSvc.Verify(t => t.Generate(
            It.IsAny<User>(), It.IsAny<IEnumerable<string>>()), Times.Never);
    }

    [Fact(DisplayName = "Não deve buscar roles antes de validar a senha")]
    public async Task Execute_WithWrongPassword_ShouldNotFetchRoles()
    {
        var user = FakeUser();
        _userRepo.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), default)).ReturnsAsync(user);
        _hasher.Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>())).Returns(false);

        try { await _sut.ExecuteAsync(new LoginDto("caique@monkeybomb.com", "Errada"), "1.1.1.1"); }
        catch { /* esperado */ }

        _roleRepo.Verify(r => r.GetRoleNamesByUserIdAsync(It.IsAny<Guid>(), default), Times.Never);
    }

    // ── Lockout de conta (#391) — defaults: 5 tentativas / 15 min ─────────────

    private void SetupUser(User user) =>
        _userRepo.Setup(r => r.GetByEmailAsync("caique@monkeybomb.com", default)).ReturnsAsync(user);

    private static User LockedUser()
    {
        var user = FakeUser();
        for (var i = 0; i < 5; i++)
            user.RegisterFailedLogin(5, TimeSpan.FromMinutes(15), DateTime.UtcNow);
        return user;
    }

    [Fact(DisplayName = "Senha incorreta deve registrar falha, atualizar e commitar")]
    public async Task Execute_WithWrongPassword_ShouldPersistFailedAttempt()
    {
        var user = FakeUser();
        SetupUser(user);
        _hasher.Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>())).Returns(false);

        var act = () => _sut.ExecuteAsync(new LoginDto("caique@monkeybomb.com", "Errada"), "1.1.1.1");

        await act.Should().ThrowAsync<DomainException>();
        user.FailedLoginCount.Should().Be(1);
        _userRepo.Verify(r => r.UpdateAsync(user, It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "Conta bloqueada deve falhar mesmo com senha correta, sem Verify do hash real nem Generate")]
    public async Task Execute_WithLockedAccount_ShouldFailWithoutVerifyOrGenerate()
    {
        var user = LockedUser();
        SetupUser(user);
        _hasher.Setup(h => h.Verify("Senha@123", "hashed_password")).Returns(true);

        var act = () => _sut.ExecuteAsync(new LoginDto("caique@monkeybomb.com", "Senha@123"), "1.1.1.1");

        await act.Should().ThrowAsync<DomainException>().WithMessage("Credenciais inválidas.");
        _hasher.Verify(h => h.Verify(It.IsAny<string>(), "hashed_password"), Times.Never);
        _tokenSvc.Verify(t => t.Generate(
            It.IsAny<User>(), It.IsAny<IEnumerable<string>>()), Times.Never);
    }

    // (D6) O limiar de 5 falhas agora vale POR PAR (conta, IP) - ver testes de par mais abaixo.

    [Fact(DisplayName = "Login com sucesso deve zerar o contador e commitar")]
    public async Task Execute_WithValidCredentials_ShouldResetFailedLogins()
    {
        var user = FakeUser();
        user.RegisterFailedLogin(5, TimeSpan.FromMinutes(15), DateTime.UtcNow);
        user.RegisterFailedLogin(5, TimeSpan.FromMinutes(15), DateTime.UtcNow);
        var roles = new[] { "User" };
        SetupUser(user);
        _hasher.Setup(h => h.Verify("Senha@123", "hashed_password")).Returns(true);
        _roleRepo.Setup(r => r.GetRoleNamesByUserIdAsync(user.Id, default)).ReturnsAsync(roles);
        _tokenSvc.Setup(t => t.Generate(user, roles)).Returns("jwt_token");

        await _sut.ExecuteAsync(new LoginDto("caique@monkeybomb.com", "Senha@123"), "1.1.1.1");

        user.FailedLoginCount.Should().Be(0);
        user.LockedUntil.Should().BeNull();
        _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "E-mail inexistente não deve atualizar nem commitar, com mensagem genérica")]
    public async Task Execute_WithUnknownEmail_ShouldNotCommit()
    {
        _userRepo.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), default))
                 .ReturnsAsync((User?)null);

        var act = () => _sut.ExecuteAsync(new LoginDto("x@x.com", "Senha@123"), "1.1.1.1");

        await act.Should().ThrowAsync<DomainException>().WithMessage("Credenciais inválidas.");
        _userRepo.Verify(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "Lockout expirado deve permitir login com senha correta")]
    public async Task Execute_WithExpiredLockout_ShouldAllowLogin()
    {
        var user = FakeUser();
        var past = DateTime.UtcNow.AddMinutes(-20);
        for (var i = 0; i < 5; i++)
            user.RegisterFailedLogin(5, TimeSpan.FromMinutes(15), past);
        var roles = new[] { "User" };
        SetupUser(user);
        _hasher.Setup(h => h.Verify("Senha@123", "hashed_password")).Returns(true);
        _roleRepo.Setup(r => r.GetRoleNamesByUserIdAsync(user.Id, default)).ReturnsAsync(roles);
        _tokenSvc.Setup(t => t.Generate(user, roles)).Returns("jwt_token");

        var result = await _sut.ExecuteAsync(new LoginDto("caique@monkeybomb.com", "Senha@123"), "1.1.1.1");

        result.Token.Should().Be("jwt_token");
        user.FailedLoginCount.Should().Be(0);
    }

    // ── Ciclo 2 (#391): D2 concorrência, D3 timing/enumeração ─────────────────

    private const string DummyHashPlaceholder = "hashed_password";

    // ConcurrencyConflictException ainda não existe (Domain) — resolvida por reflexão
    // para o Red falhar por comportamento ausente e não por erro de compilação.
    private static Exception NewConcurrencyConflict()
    {
        var type = Type.GetType(
            "PersonalFinance.Domain.Exceptions.ConcurrencyConflictException, PersonalFinance.Domain");
        if (type is null)
            throw new InvalidOperationException(
                "PersonalFinance.Domain.Exceptions.ConcurrencyConflictException não existe.");
        return (Exception)Activator.CreateInstance(type)!;
    }

    [Fact(DisplayName = "Conta bloqueada com senha correta deve verificar hash dummy, nunca o hash real")]
    public async Task Execute_WithLockedAccount_ShouldVerifyDummyHashOnly()
    {
        var user = LockedUser();
        SetupUser(user);
        _hasher.Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>())).Returns(true);

        var act = () => _sut.ExecuteAsync(new LoginDto("caique@monkeybomb.com", "Senha@123"), "1.1.1.1");

        await act.Should().ThrowAsync<DomainException>().WithMessage("Credenciais inválidas.");
        _hasher.Verify(h => h.Verify("Senha@123", DummyHashPlaceholder), Times.Never);
        _hasher.Verify(h => h.Verify("Senha@123",
            It.Is<string>(x => !string.IsNullOrEmpty(x) && x != DummyHashPlaceholder)), Times.Once);
        _tokenSvc.Verify(t => t.Generate(
            It.IsAny<User>(), It.IsAny<IEnumerable<string>>()), Times.Never);
    }

    [Fact(DisplayName = "E-mail inexistente deve executar Verify contra hash dummy")]
    public async Task Execute_WithUnknownEmail_ShouldVerifyDummyHash()
    {
        _userRepo.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), default))
                 .ReturnsAsync((User?)null);
        _hasher.Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>())).Returns(true);

        var act = () => _sut.ExecuteAsync(new LoginDto("x@x.com", "Senha@123"), "1.1.1.1");

        await act.Should().ThrowAsync<DomainException>().WithMessage("Credenciais inválidas.");
        _hasher.Verify(h => h.Verify("Senha@123",
            It.Is<string>(x => !string.IsNullOrEmpty(x))), Times.Once);
    }

    [Fact(DisplayName = "Usuário inativo deve falhar com mensagem genérica")]
    public async Task Execute_WithInactiveUser_ShouldUseGenericMessage()
    {
        var user = FakeUser();
        user.SoftDelete();
        SetupUser(user);

        var act = () => _sut.ExecuteAsync(new LoginDto("caique@monkeybomb.com", "Senha@123"), "1.1.1.1");

        var ex = await act.Should().ThrowAsync<DomainException>();
        ex.Which.Message.Should().Be("Credenciais inválidas.");
        ex.Which.Message.Should().NotContain("inativo");
    }

    [Fact(DisplayName = "Conflito de concorrência na falha de senha deve recarregar o usuário e reaplicar na 2ª tentativa")]
    public async Task Execute_WrongPassword_ConflictThenSuccess_ShouldReloadAndRetry()
    {
        var stale = FakeUser();
        var fresh = FakeUser();
        _userRepo.SetupSequence(r => r.GetByEmailAsync("caique@monkeybomb.com", default))
                 .ReturnsAsync(stale)
                 .ReturnsAsync(fresh);
        _hasher.Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>())).Returns(false);
        _uow.SetupSequence(u => u.CommitAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(NewConcurrencyConflict())
            .Returns(Task.CompletedTask);

        var act = () => _sut.ExecuteAsync(new LoginDto("caique@monkeybomb.com", "Errada"), "1.1.1.1");

        await act.Should().ThrowAsync<DomainException>().WithMessage("Credenciais inválidas.");
        _userRepo.Verify(r => r.GetByEmailAsync("caique@monkeybomb.com", default), Times.Exactly(2));
        _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
        fresh.FailedLoginCount.Should().Be(1);
    }

    [Fact(DisplayName = "Conflito persistente deve esgotar 3 tentativas e lançar DomainException sem vazar o conflito")]
    public async Task Execute_WrongPassword_PersistentConflict_ShouldStopAfterThreeAttempts()
    {
        _userRepo.Setup(r => r.GetByEmailAsync("caique@monkeybomb.com", default))
                 .ReturnsAsync(() => FakeUser());
        _hasher.Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>())).Returns(false);
        _uow.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(NewConcurrencyConflict());

        var act = () => _sut.ExecuteAsync(new LoginDto("caique@monkeybomb.com", "Errada"), "1.1.1.1");

        var ex = await act.Should().ThrowAsync<DomainException>();
        ex.Which.Message.Should().Be("Credenciais inválidas.");
        _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [Fact(DisplayName = "Conflito ao zerar contador no sucesso deve tentar de novo e retornar token")]
    public async Task Execute_SuccessResetConflict_ShouldRetryAndReturnToken()
    {
        var roles = new[] { "User" };
        var stale = FakeUser();
        stale.RegisterFailedLogin(5, TimeSpan.FromMinutes(15), DateTime.UtcNow);
        var fresh = FakeUser();
        fresh.RegisterFailedLogin(5, TimeSpan.FromMinutes(15), DateTime.UtcNow);
        _userRepo.SetupSequence(r => r.GetByEmailAsync("caique@monkeybomb.com", default))
                 .ReturnsAsync(stale)
                 .ReturnsAsync(fresh);
        _hasher.Setup(h => h.Verify("Senha@123", "hashed_password")).Returns(true);
        _roleRepo.Setup(r => r.GetRoleNamesByUserIdAsync(It.IsAny<Guid>(), default)).ReturnsAsync(roles);
        _tokenSvc.Setup(t => t.Generate(It.IsAny<User>(), roles)).Returns("jwt_token");
        _uow.SetupSequence(u => u.CommitAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(NewConcurrencyConflict())
            .Returns(Task.CompletedTask);

        var result = await _sut.ExecuteAsync(new LoginDto("caique@monkeybomb.com", "Senha@123"), "1.1.1.1");

        result.Token.Should().Be("jwt_token");
        fresh.FailedLoginCount.Should().Be(0);
        _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    // ── Ciclo 2 (#391): D3b e D6 — lockout por (conta, IP) + teto global por conta ──

    private const string Email = "caique@monkeybomb.com";
    private const string IpA = "1.1.1.1";
    private const string IpB = "2.2.2.2";

    /// <summary>
    /// Simula o repositório de pares em memória (GetAsync/TryAddAsync/RemoveAsync),
    /// indexado por (userId, ip). TryAddAsync sempre aceita o par.
    /// </summary>
    private Dictionary<(Guid, string), LoginThrottle> SetupStatefulThrottles()
    {
        var rows = new Dictionary<(Guid, string), LoginThrottle>();
        _throttleRepo.Setup(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, string ip, CancellationToken _) =>
                rows.TryGetValue((id, ip), out var t) ? t : null);
        _throttleRepo.Setup(r => r.TryAddAsync(
                It.IsAny<LoginThrottle>(), It.IsAny<DateTime>(), It.IsAny<TimeSpan>(),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LoginThrottle t, DateTime _, TimeSpan _, int _, int _, CancellationToken _) =>
            {
                rows[(t.UserId, t.IpAddress)] = t;
                return true;
            });
        _throttleRepo.Setup(r => r.RemoveAsync(It.IsAny<LoginThrottle>(), It.IsAny<CancellationToken>()))
            .Callback((LoginThrottle t, CancellationToken _) => rows.Remove((t.UserId, t.IpAddress)))
            .Returns(Task.CompletedTask);
        return rows;
    }

    private async Task FailAsync(string ip, string email = Email)
    {
        try { await _sut.ExecuteAsync(new LoginDto(email, "Errada"), ip); }
        catch (DomainException) { /* esperado */ }
    }

    private void SetupSuccessfulLoginFor(User user)
    {
        var roles = new[] { "User" };
        _hasher.Setup(h => h.Verify("Senha@123", "hashed_password")).Returns(true);
        _roleRepo.Setup(r => r.GetRoleNamesByUserIdAsync(user.Id, default)).ReturnsAsync(roles);
        _tokenSvc.Setup(t => t.Generate(user, roles)).Returns("jwt_token");
    }

    [Fact(DisplayName = "D3b: usuário inativo/deletado deve executar Verify do hash dummy uma vez, nunca o hash real")]
    public async Task Execute_WithInactiveUser_ShouldVerifyDummyHashOnly()
    {
        var user = FakeUser();
        user.SoftDelete();
        SetupUser(user);
        _hasher.Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>())).Returns(true);

        var act = () => _sut.ExecuteAsync(new LoginDto(Email, "Senha@123"), IpA);

        await act.Should().ThrowAsync<DomainException>().WithMessage("Credenciais inválidas.");
        _hasher.Verify(h => h.Verify(It.IsAny<string>(), "hashed_password"), Times.Never);
        _hasher.Verify(h => h.Verify("Senha@123",
            It.Is<string>(x => !string.IsNullOrEmpty(x) && x != "hashed_password")), Times.Once);
        _tokenSvc.Verify(t => t.Generate(
            It.IsAny<User>(), It.IsAny<IEnumerable<string>>()), Times.Never);
    }

    [Fact(DisplayName = "D3b: usuário desativado (Deactivate) também deve usar Verify dummy")]
    public async Task Execute_WithDeactivatedUser_ShouldVerifyDummyHashOnly()
    {
        var user = FakeUser();
        user.Deactivate();
        SetupUser(user);

        var act = () => _sut.ExecuteAsync(new LoginDto(Email, "Senha@123"), IpA);

        await act.Should().ThrowAsync<DomainException>().WithMessage("Credenciais inválidas.");
        _hasher.Verify(h => h.Verify(It.IsAny<string>(), "hashed_password"), Times.Never);
        _hasher.Verify(h => h.Verify("Senha@123", It.Is<string>(x => !string.IsNullOrEmpty(x))), Times.Once);
    }

    [Fact(DisplayName = "Primeira falha de um par novo deve inserir o par via TryAddAsync com contador 1 e incrementar o teto global do usuário")]
    public async Task Execute_WrongPassword_NewPair_ShouldTryAddPairAndIncrementUser()
    {
        var user = FakeUser();
        SetupUser(user);
        _hasher.Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>())).Returns(false);

        await FailAsync(IpA);

        _throttleRepo.Verify(r => r.TryAddAsync(
            It.Is<LoginThrottle>(t => t.UserId == user.Id && t.IpAddress == IpA && t.FailedCount == 1),
            It.IsAny<DateTime>(), TimeSpan.FromMinutes(15), 2000, 500,
            It.IsAny<CancellationToken>()), Times.Once);
        user.FailedLoginCount.Should().Be(1);
        _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "Falha em par existente deve incrementar e atualizar a linha, sem novo TryAddAsync")]
    public async Task Execute_WrongPassword_ExistingPair_ShouldUpdateRow()
    {
        var user = FakeUser();
        SetupUser(user);
        var row = LoginThrottle.Create(user.Id, IpA, DateTime.UtcNow);
        _throttleRepo.Setup(r => r.GetAsync(user.Id, IpA, It.IsAny<CancellationToken>())).ReturnsAsync(row);
        _hasher.Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>())).Returns(false);

        await FailAsync(IpA);

        row.FailedCount.Should().Be(1);
        _throttleRepo.Verify(r => r.UpdateAsync(row, It.IsAny<CancellationToken>()), Times.Once);
        _throttleRepo.Verify(r => r.TryAddAsync(
            It.IsAny<LoginThrottle>(), It.IsAny<DateTime>(), It.IsAny<TimeSpan>(),
            It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "Par: 5 falhas seguidas do mesmo IP bloqueiam o par, mas não o usuário (teto global é 50)")]
    public async Task Execute_FiveFailuresSameIp_ShouldLockPairNotUser()
    {
        var user = FakeUser();
        SetupUser(user);
        var rows = SetupStatefulThrottles();
        _hasher.Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>())).Returns(false);

        for (var i = 0; i < 5; i++) await FailAsync(IpA);

        rows[(user.Id, IpA)].IsLockedOut(DateTime.UtcNow).Should().BeTrue();
        user.IsLockedOut(DateTime.UtcNow).Should().BeFalse();
        user.FailedLoginCount.Should().Be(5);
    }

    [Fact(DisplayName = "Par bloqueado deve falhar com senha correta: Verify só do hash dummy, sem token")]
    public async Task Execute_LockedPair_ShouldFailWithDummyVerifyOnly()
    {
        var user = FakeUser();
        SetupUser(user);
        var rows = SetupStatefulThrottles();
        _hasher.Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>())).Returns(false);
        for (var i = 0; i < 5; i++) await FailAsync(IpA);
        _hasher.Invocations.Clear();
        _hasher.Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>())).Returns(true);

        var act = () => _sut.ExecuteAsync(new LoginDto(Email, "Senha@123"), IpA);

        await act.Should().ThrowAsync<DomainException>().WithMessage("Credenciais inválidas.");
        _hasher.Verify(h => h.Verify(It.IsAny<string>(), "hashed_password"), Times.Never);
        _hasher.Verify(h => h.Verify("Senha@123", It.Is<string>(x => !string.IsNullOrEmpty(x))), Times.Once);
        _tokenSvc.Verify(t => t.Generate(
            It.IsAny<User>(), It.IsAny<IEnumerable<string>>()), Times.Never);
        rows.Should().ContainKey((user.Id, IpA));
    }

    [Fact(DisplayName = "IP diferente de um par bloqueado não é bloqueado: login correto do outro IP funciona")]
    public async Task Execute_LockedPair_OtherIpShouldStillLogin()
    {
        var user = FakeUser();
        SetupUser(user);
        SetupStatefulThrottles();
        _hasher.Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>())).Returns(false);
        for (var i = 0; i < 5; i++) await FailAsync(IpA);
        SetupSuccessfulLoginFor(user);

        var result = await _sut.ExecuteAsync(new LoginDto(Email, "Senha@123"), IpB);

        result.Token.Should().Be("jwt_token");
    }

    [Fact(DisplayName = "Teto global: falhas de IPs diferentes somam no usuário e bloqueiam qualquer IP ao atingir GlobalMaxFailedAttempts")]
    public async Task Execute_GlobalCeiling_ShouldLockAccountForAnyIp()
    {
        var options = new LoginLockoutOptions { MaxFailedAttempts = 5, GlobalMaxFailedAttempts = 3, LockoutMinutes = 15 };
        var sut = new LoginWithRolesUseCase(
            _userRepo.Object, _roleRepo.Object, _throttleRepo.Object,
            _hasher.Object, _tokenSvc.Object, _uow.Object, options, new MfaOptions(), _emailOptions, TestValidators.Valid<LoginDto>());
        var user = FakeUser();
        SetupUser(user);
        SetupStatefulThrottles();
        _hasher.Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>())).Returns(false);

        foreach (var ip in new[] { "3.3.3.3", "4.4.4.4", "5.5.5.5" })
        {
            try { await sut.ExecuteAsync(new LoginDto(Email, "Errada"), ip); }
            catch (DomainException) { /* esperado */ }
        }
        SetupSuccessfulLoginFor(user);

        var act = () => sut.ExecuteAsync(new LoginDto(Email, "Senha@123"), "6.6.6.6");

        await act.Should().ThrowAsync<DomainException>().WithMessage("Credenciais inválidas.");
        user.IsLockedOut(DateTime.UtcNow).Should().BeTrue();
        _tokenSvc.Verify(t => t.Generate(
            It.IsAny<User>(), It.IsAny<IEnumerable<string>>()), Times.Never);
    }

    [Fact(DisplayName = "Conta bloqueada globalmente não usa o hash real, mesmo com par sem bloqueio")]
    public async Task Execute_GloballyLockedUser_ShouldNotVerifyRealHash()
    {
        var user = FakeUser();
        for (var i = 0; i < 50; i++)
            user.RegisterFailedLogin(50, TimeSpan.FromMinutes(15), DateTime.UtcNow);
        SetupUser(user);
        SetupStatefulThrottles();
        _hasher.Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>())).Returns(true);

        var act = () => _sut.ExecuteAsync(new LoginDto(Email, "Senha@123"), IpB);

        await act.Should().ThrowAsync<DomainException>().WithMessage("Credenciais inválidas.");
        _hasher.Verify(h => h.Verify(It.IsAny<string>(), "hashed_password"), Times.Never);
    }

    [Fact(DisplayName = "TryAddAsync recusando o par (fail-open) deve seguir só com o teto global e ainda commitar")]
    public async Task Execute_TryAddRejected_ShouldFallBackToUserCounter()
    {
        var user = FakeUser();
        SetupUser(user);
        _throttleRepo.Setup(r => r.TryAddAsync(
                It.IsAny<LoginThrottle>(), It.IsAny<DateTime>(), It.IsAny<TimeSpan>(),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _hasher.Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>())).Returns(false);

        var act = () => _sut.ExecuteAsync(new LoginDto(Email, "Errada"), IpA);

        await act.Should().ThrowAsync<DomainException>().WithMessage("Credenciais inválidas.");
        user.FailedLoginCount.Should().Be(1);
        _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "E-mail inexistente não deve tocar no repositório de pares (nada gravado)")]
    public async Task Execute_UnknownEmail_ShouldNotTouchThrottleRepository()
    {
        _userRepo.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), default)).ReturnsAsync((User?)null);

        await FailAsync(IpA, "x@x.com");

        _throttleRepo.VerifyNoOtherCalls();
        _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "Usuário inativo não deve gravar par nem commitar")]
    public async Task Execute_InactiveUser_ShouldNotWriteThrottle()
    {
        var user = FakeUser();
        user.SoftDelete();
        SetupUser(user);

        await FailAsync(IpA);

        _throttleRepo.Verify(r => r.TryAddAsync(
            It.IsAny<LoginThrottle>(), It.IsAny<DateTime>(), It.IsAny<TimeSpan>(),
            It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _throttleRepo.Verify(r => r.UpdateAsync(It.IsAny<LoginThrottle>(), It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "Sucesso deve zerar o contador do usuário e remover a linha do par")]
    public async Task Execute_Success_ShouldResetUserAndRemovePair()
    {
        var user = FakeUser();
        user.RegisterFailedLogin(50, TimeSpan.FromMinutes(15), DateTime.UtcNow);
        SetupUser(user);
        var row = LoginThrottle.Create(user.Id, IpA, DateTime.UtcNow);
        row.RegisterFailure(5, TimeSpan.FromMinutes(15), DateTime.UtcNow);
        _throttleRepo.Setup(r => r.GetAsync(user.Id, IpA, It.IsAny<CancellationToken>())).ReturnsAsync(row);
        SetupSuccessfulLoginFor(user);

        var result = await _sut.ExecuteAsync(new LoginDto(Email, "Senha@123"), IpA);

        result.Token.Should().Be("jwt_token");
        user.FailedLoginCount.Should().Be(0);
        _throttleRepo.Verify(r => r.RemoveAsync(row, It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "Sucesso sem falhas anteriores não deve commitar nem remover pares")]
    public async Task Execute_SuccessWithoutHistory_ShouldNotCommit()
    {
        var user = FakeUser();
        SetupUser(user);
        SetupSuccessfulLoginFor(user);

        await _sut.ExecuteAsync(new LoginDto(Email, "Senha@123"), IpA);

        _throttleRepo.Verify(r => r.RemoveAsync(It.IsAny<LoginThrottle>(), It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "Conflito de concorrência ao registrar o par deve recarregar e reaplicar (retry do D2)")]
    public async Task Execute_PairConflict_ShouldRetry()
    {
        var stale = FakeUser();
        var fresh = FakeUser();
        _userRepo.SetupSequence(r => r.GetByEmailAsync(Email, default))
                 .ReturnsAsync(stale).ReturnsAsync(fresh);
        SetupStatefulThrottles();
        _hasher.Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>())).Returns(false);
        _uow.SetupSequence(u => u.CommitAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(NewConcurrencyConflict())
            .Returns(Task.CompletedTask);

        await FailAsync(IpA);

        _userRepo.Verify(r => r.GetByEmailAsync(Email, default), Times.Exactly(2));
        _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
        fresh.FailedLoginCount.Should().Be(1);
    }

    // ── MFA no login (#393) — 2º fator, flag Auth:Mfa:Enforce ─────────────────

    private static User MfaUser()
    {
        var user = FakeUser();
        user.SetPendingMfaSecret("cipher-blob");
        user.EnableMfa(DateTime.UtcNow);
        return user;
    }

    [Fact(DisplayName = "MFA: Enforce=true com MFA ativo deve retornar MfaRequired e MfaToken, sem Token completo")]
    public async Task Execute_MfaEnforcedAndEnabled_ShouldReturnChallenge()
    {
        _mfaOptions.Enforce = true;
        var user = MfaUser();
        SetupUser(user);
        _hasher.Setup(h => h.Verify("Senha@123", "hashed_password")).Returns(true);
        _tokenSvc.Setup(t => t.GenerateMfaChallenge(user)).Returns("challenge-token");

        var result = await _sut.ExecuteAsync(new LoginDto(Email, "Senha@123"), IpA);

        result.MfaRequired.Should().BeTrue();
        result.MfaToken.Should().Be("challenge-token");
        result.Token.Should().BeNull();
        result.Email.Should().Be(Email);
        _tokenSvc.Verify(t => t.Generate(It.IsAny<User>(), It.IsAny<IEnumerable<string>>()), Times.Never);
    }

    [Fact(DisplayName = "MFA: Enforce=true com MFA ativo não deve zerar contadores nem remover throttle (só após o 2º fator)")]
    public async Task Execute_MfaEnforcedAndEnabled_ShouldNotResetCounters()
    {
        _mfaOptions.Enforce = true;
        var user = MfaUser();
        user.RegisterFailedLogin(5, TimeSpan.FromMinutes(15), DateTime.UtcNow);
        user.RegisterFailedLogin(5, TimeSpan.FromMinutes(15), DateTime.UtcNow);
        var throttle = LoginThrottle.Create(user.Id, IpA, DateTime.UtcNow);
        throttle.RegisterFailure(5, TimeSpan.FromMinutes(15), DateTime.UtcNow);
        SetupUser(user);
        _throttleRepo.Setup(r => r.GetAsync(user.Id, IpA, It.IsAny<CancellationToken>())).ReturnsAsync(throttle);
        _hasher.Setup(h => h.Verify("Senha@123", "hashed_password")).Returns(true);
        _tokenSvc.Setup(t => t.GenerateMfaChallenge(user)).Returns("challenge-token");

        await _sut.ExecuteAsync(new LoginDto(Email, "Senha@123"), IpA);

        user.FailedLoginCount.Should().Be(2);
        throttle.FailedCount.Should().Be(1);
        _throttleRepo.Verify(r => r.RemoveAsync(It.IsAny<LoginThrottle>(), It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "MFA: Enforce=false com MFA ativo deve manter o login normal (flag desligada)")]
    public async Task Execute_MfaNotEnforcedButEnabled_ShouldLoginNormally()
    {
        _mfaOptions.Enforce = false;
        var user = MfaUser();
        SetupUser(user);
        SetupSuccessfulLoginFor(user);

        var result = await _sut.ExecuteAsync(new LoginDto(Email, "Senha@123"), IpA);

        result.Token.Should().Be("jwt_token");
        result.MfaRequired.Should().BeFalse();
        result.MfaToken.Should().BeNull();
        _tokenSvc.Verify(t => t.GenerateMfaChallenge(It.IsAny<User>()), Times.Never);
    }

    [Fact(DisplayName = "MFA: Enforce=true com usuário sem MFA deve manter o login normal")]
    public async Task Execute_MfaEnforcedButUserWithoutMfa_ShouldLoginNormally()
    {
        _mfaOptions.Enforce = true;
        var user = FakeUser();
        SetupUser(user);
        SetupSuccessfulLoginFor(user);

        var result = await _sut.ExecuteAsync(new LoginDto(Email, "Senha@123"), IpA);

        result.Token.Should().Be("jwt_token");
        result.MfaRequired.Should().BeFalse();
        _tokenSvc.Verify(t => t.GenerateMfaChallenge(It.IsAny<User>()), Times.Never);
    }

    [Fact(DisplayName = "MFA: Enforce=true com setup pendente (não ativado) deve manter o login normal")]
    public async Task Execute_MfaEnforcedButOnlyPendingSecret_ShouldLoginNormally()
    {
        _mfaOptions.Enforce = true;
        var user = FakeUser();
        user.SetPendingMfaSecret("cipher-blob");
        SetupUser(user);
        SetupSuccessfulLoginFor(user);

        var result = await _sut.ExecuteAsync(new LoginDto(Email, "Senha@123"), IpA);

        result.Token.Should().Be("jwt_token");
        result.MfaRequired.Should().BeFalse();
    }

    [Fact(DisplayName = "MFA: senha errada com MFA ativo deve manter a mensagem genérica, contar a falha e não emitir challenge")]
    public async Task Execute_MfaEnforcedWrongPassword_ShouldFailGenericallyAndCount()
    {
        _mfaOptions.Enforce = true;
        var user = MfaUser();
        SetupUser(user);
        _hasher.Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>())).Returns(false);

        var act = () => _sut.ExecuteAsync(new LoginDto(Email, "Errada"), IpA);

        await act.Should().ThrowAsync<DomainException>().WithMessage("Credenciais inválidas.");
        user.FailedLoginCount.Should().Be(1);
        _tokenSvc.Verify(t => t.GenerateMfaChallenge(It.IsAny<User>()), Times.Never);
    }

    [Fact(DisplayName = "MFA: conta bloqueada com MFA ativo deve falhar com mensagem genérica, sem challenge")]
    public async Task Execute_MfaEnforcedLockedAccount_ShouldNotIssueChallenge()
    {
        _mfaOptions.Enforce = true;
        var user = MfaUser();
        for (var i = 0; i < 5; i++)
            user.RegisterFailedLogin(5, TimeSpan.FromMinutes(15), DateTime.UtcNow);
        SetupUser(user);
        _hasher.Setup(h => h.Verify("Senha@123", "hashed_password")).Returns(true);

        var act = () => _sut.ExecuteAsync(new LoginDto(Email, "Senha@123"), IpA);

        await act.Should().ThrowAsync<DomainException>().WithMessage("Credenciais inválidas.");
        _tokenSvc.Verify(t => t.GenerateMfaChallenge(It.IsAny<User>()), Times.Never);
    }

    // ── Verificação de e-mail no login (#404) — flag Auth:EmailVerification:Enforce ──────

    private void SetupCorrectPassword(User user)
    {
        _userRepo.Setup(r => r.GetByEmailAsync("caique@monkeybomb.com", default)).ReturnsAsync(user);
        _hasher.Setup(h => h.Verify("Senha@123", "hashed_password")).Returns(true);
        _roleRepo.Setup(r => r.GetRoleNamesByUserIdAsync(user.Id, default)).ReturnsAsync(new[] { "User" });
        _tokenSvc.Setup(t => t.Generate(user, It.IsAny<IEnumerable<string>>())).Returns("jwt_token");
    }

    [Fact(DisplayName = "E-mail: Enforce=false (default) com e-mail não verificado e senha correta deve logar normalmente")]
    public async Task Execute_EmailEnforceOff_UnverifiedUser_ShouldLogin()
    {
        _emailOptions.Enforce = false;
        var user = FakeUser();
        SetupCorrectPassword(user);

        var result = await _sut.ExecuteAsync(new LoginDto("caique@monkeybomb.com", "Senha@123"), "1.1.1.1");

        result.Token.Should().Be("jwt_token");
    }

    [Fact(DisplayName = "E-mail: Enforce=true com e-mail não verificado e senha correta deve recusar com a mensagem genérica")]
    public async Task Execute_EmailEnforceOn_UnverifiedUser_ShouldRejectWithGenericMessage()
    {
        _emailOptions.Enforce = true;
        var user = FakeUser();
        SetupCorrectPassword(user);

        var act = () => _sut.ExecuteAsync(new LoginDto("caique@monkeybomb.com", "Senha@123"), "1.1.1.1");

        await act.Should().ThrowAsync<DomainException>().WithMessage("Credenciais inválidas.");
        _tokenSvc.Verify(t => t.Generate(It.IsAny<User>(), It.IsAny<IEnumerable<string>>()), Times.Never);
    }

    [Fact(DisplayName = "E-mail: Enforce=true com e-mail verificado deve logar normalmente")]
    public async Task Execute_EmailEnforceOn_VerifiedUser_ShouldLogin()
    {
        _emailOptions.Enforce = true;
        var user = FakeUser();
        user.ConfirmEmail(DateTime.UtcNow);
        SetupCorrectPassword(user);

        var result = await _sut.ExecuteAsync(new LoginDto("caique@monkeybomb.com", "Senha@123"), "1.1.1.1");

        result.Token.Should().Be("jwt_token");
    }

    [Fact(DisplayName = "E-mail: Enforce=true e não verificado com MFA ativo não deve emitir challenge de MFA")]
    public async Task Execute_EmailEnforceOn_UnverifiedUserWithMfa_ShouldNotIssueMfaChallenge()
    {
        _emailOptions.Enforce = true;
        _mfaOptions.Enforce = true;
        var user = MfaUser();
        SetupCorrectPassword(user);

        var act = () => _sut.ExecuteAsync(new LoginDto("caique@monkeybomb.com", "Senha@123"), "1.1.1.1");

        await act.Should().ThrowAsync<DomainException>().WithMessage("Credenciais inválidas.");
        _tokenSvc.Verify(t => t.GenerateMfaChallenge(It.IsAny<User>()), Times.Never);
    }

    [Fact(DisplayName = "E-mail: Enforce=true com senha ERRADA mantém o fluxo de falha (contador e mesma mensagem)")]
    public async Task Execute_EmailEnforceOn_WrongPassword_ShouldKeepFailureFlow()
    {
        _emailOptions.Enforce = true;
        var user = FakeUser();
        _userRepo.Setup(r => r.GetByEmailAsync("caique@monkeybomb.com", default)).ReturnsAsync(user);
        _hasher.Setup(h => h.Verify("Errada", "hashed_password")).Returns(false);

        var act = () => _sut.ExecuteAsync(new LoginDto("caique@monkeybomb.com", "Errada"), "1.1.1.1");

        await act.Should().ThrowAsync<DomainException>().WithMessage("Credenciais inválidas.");
        user.FailedLoginCount.Should().Be(1);
    }

    // ── Validação (#396) ──────────────────────────────────────────────────────

    private LoginWithRolesUseCase SutWith(IValidator<LoginDto> validator) =>
        UseCaseFactory.Create<LoginWithRolesUseCase>(
            _userRepo.Object, _roleRepo.Object, _throttleRepo.Object,
            _hasher.Object, _tokenSvc.Object, _uow.Object,
            new LoginLockoutOptions(), _mfaOptions, _emailOptions, validator);

    [Fact(DisplayName = "Deve lançar ValidationException quando o validator reprova o DTO")]
    public async Task Execute_WhenValidatorFails_ShouldThrowValidationException()
    {
        var sut = SutWith(TestValidators.Invalid<LoginDto>());

        var act = () => sut.ExecuteAsync(new LoginDto("caique@monkeybomb.com", "Senha@123"), "1.1.1.1");

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact(DisplayName = "Não deve consultar usuário nem throttle quando o validator reprova")]
    public async Task Execute_WhenValidatorFails_ShouldNotTouchRepositories()
    {
        var sut = SutWith(TestValidators.Invalid<LoginDto>());

        await Assert.ThrowsAsync<ValidationException>(
            () => sut.ExecuteAsync(new LoginDto("caique@monkeybomb.com", "Senha@123"), "1.1.1.1"));

        _userRepo.Invocations.Should().BeEmpty();
        _throttleRepo.Invocations.Should().BeEmpty();
        _tokenSvc.Invocations.Should().BeEmpty();
    }
}
