using FluentAssertions;
using Moq;
using PersonalFinance.Application.DTOs.Auth;
using PersonalFinance.Application.UseCases.Auth;
using PersonalFinance.Domain.Entities.Auth;
using PersonalFinance.Domain.Exceptions;
using PersonalFinance.Domain.Interfaces.Repositories;
using PersonalFinance.Domain.Interfaces.Services;
using Xunit;

namespace PersonalFinance.Application.Tests.Unit.Auth;

public class LoginWithRolesUseCaseTests
{
    private readonly Mock<IUserRepository>     _userRepo  = new();
    private readonly Mock<IUserRoleRepository> _roleRepo  = new();
    private readonly Mock<IPasswordHasher>     _hasher    = new();
    private readonly Mock<ITokenService>       _tokenSvc  = new();
    private readonly LoginWithRolesUseCase     _sut;

    public LoginWithRolesUseCaseTests()
    {
        _sut = new LoginWithRolesUseCase(
            _userRepo.Object, _roleRepo.Object,
            _hasher.Object,   _tokenSvc.Object);
    }

    private static User FakeUser() =>
        User.Create("Caique", "caique@monkeybomb.com", "hashed_password");

    [Fact(DisplayName = "Deve retornar token com roles para credenciais válidas")]
    public async Task Execute_WithValidCredentials_ShouldReturnTokenWithRoles()
    {
        var user  = FakeUser();
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
        var user  = FakeUser();
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
}
