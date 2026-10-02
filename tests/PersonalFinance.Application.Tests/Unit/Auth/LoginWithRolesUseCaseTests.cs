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
using Xunit;

namespace PersonalFinance.Application.Tests.Unit.Auth;

public class LoginWithRolesUseCaseTests
{
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<IUserRoleRepository> _roleRepo = new();
    private readonly Mock<IPasswordHasher> _hasher = new();
    private readonly Mock<ITokenService> _tokenSvc = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly LoginWithRolesUseCase _sut;

    public LoginWithRolesUseCaseTests()
    {
        _sut = new LoginWithRolesUseCase(
            _userRepo.Object, _roleRepo.Object,
            _hasher.Object, _tokenSvc.Object,
            _uow.Object, new LoginLockoutOptions());
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

        var result = await _sut.ExecuteAsync(new LoginDto("caique@monkeybomb.com", "Senha@123"));

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

        var result = await _sut.ExecuteAsync(new LoginDto("caique@monkeybomb.com", "Senha@123"));

        result.Token.Should().Be("admin_token");
        _tokenSvc.Verify(t => t.Generate(user, roles), Times.Once);
    }

    [Fact(DisplayName = "Deve lançar exceção para e-mail não cadastrado")]
    public async Task Execute_WithUnknownEmail_ShouldThrow()
    {
        _userRepo.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), default))
                 .ReturnsAsync((User?)null);

        var act = () => _sut.ExecuteAsync(new LoginDto("x@x.com", "Senha@123"));

        await act.Should().ThrowAsync<DomainException>().WithMessage("*credenciais*");
    }

    [Fact(DisplayName = "Deve lançar exceção para senha incorreta")]
    public async Task Execute_WithWrongPassword_ShouldThrow()
    {
        var user = FakeUser();
        _userRepo.Setup(r => r.GetByEmailAsync("caique@monkeybomb.com", default)).ReturnsAsync(user);
        _hasher.Setup(h => h.Verify("Errada", "hashed_password")).Returns(false);

        var act = () => _sut.ExecuteAsync(new LoginDto("caique@monkeybomb.com", "Errada"));

        await act.Should().ThrowAsync<DomainException>().WithMessage("*credenciais*");
    }

    [Fact(DisplayName = "Deve lançar exceção para usuário inativo")]
    public async Task Execute_WithInactiveUser_ShouldThrow()
    {
        var user = FakeUser();
        user.SoftDelete();
        _userRepo.Setup(r => r.GetByEmailAsync("caique@monkeybomb.com", default)).ReturnsAsync(user);

        var act = () => _sut.ExecuteAsync(new LoginDto("caique@monkeybomb.com", "Senha@123"));

        await act.Should().ThrowAsync<DomainException>().WithMessage("*inativo*");
    }

    [Fact(DisplayName = "Não deve gerar token se senha for inválida")]
    public async Task Execute_WithWrongPassword_ShouldNotGenerateToken()
    {
        var user = FakeUser();
        _userRepo.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), default)).ReturnsAsync(user);
        _hasher.Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>())).Returns(false);

        try { await _sut.ExecuteAsync(new LoginDto("caique@monkeybomb.com", "Errada")); }
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

        try { await _sut.ExecuteAsync(new LoginDto("caique@monkeybomb.com", "Errada")); }
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

        var act = () => _sut.ExecuteAsync(new LoginDto("caique@monkeybomb.com", "Errada"));

        await act.Should().ThrowAsync<DomainException>();
        user.FailedLoginCount.Should().Be(1);
        _userRepo.Verify(r => r.UpdateAsync(user, It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "Conta bloqueada deve falhar mesmo com senha correta, sem Verify nem Generate")]
    public async Task Execute_WithLockedAccount_ShouldFailWithoutVerifyOrGenerate()
    {
        var user = LockedUser();
        SetupUser(user);
        _hasher.Setup(h => h.Verify("Senha@123", "hashed_password")).Returns(true);

        var act = () => _sut.ExecuteAsync(new LoginDto("caique@monkeybomb.com", "Senha@123"));

        await act.Should().ThrowAsync<DomainException>().WithMessage("Credenciais inválidas.");
        _hasher.Verify(h => h.Verify(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _tokenSvc.Verify(t => t.Generate(
            It.IsAny<User>(), It.IsAny<IEnumerable<string>>()), Times.Never);
    }

    [Fact(DisplayName = "Quinta falha seguida deve bloquear a conta")]
    public async Task Execute_FifthConsecutiveFailure_ShouldLockAccount()
    {
        var user = FakeUser();
        SetupUser(user);
        _hasher.Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>())).Returns(false);

        for (var i = 0; i < 5; i++)
        {
            try { await _sut.ExecuteAsync(new LoginDto("caique@monkeybomb.com", "Errada")); }
            catch (DomainException) { /* esperado */ }
        }

        user.IsLockedOut(DateTime.UtcNow).Should().BeTrue();
    }

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

        await _sut.ExecuteAsync(new LoginDto("caique@monkeybomb.com", "Senha@123"));

        user.FailedLoginCount.Should().Be(0);
        user.LockedUntil.Should().BeNull();
        _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "E-mail inexistente não deve atualizar nem commitar, com mensagem genérica")]
    public async Task Execute_WithUnknownEmail_ShouldNotCommit()
    {
        _userRepo.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), default))
                 .ReturnsAsync((User?)null);

        var act = () => _sut.ExecuteAsync(new LoginDto("x@x.com", "Senha@123"));

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

        var result = await _sut.ExecuteAsync(new LoginDto("caique@monkeybomb.com", "Senha@123"));

        result.Token.Should().Be("jwt_token");
        user.FailedLoginCount.Should().Be(0);
    }
}
