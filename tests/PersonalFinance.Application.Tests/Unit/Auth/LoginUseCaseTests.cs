using FluentAssertions;
using Moq;
using PersonalFinance.Application.DTOs.Auth;
using PersonalFinance.Application.UseCases.Admin;
using PersonalFinance.Domain.Entities.Auth;
using PersonalFinance.Domain.Exceptions;
using PersonalFinance.Domain.Interfaces.Repositories;
using PersonalFinance.Domain.Interfaces.Services;
using Xunit;

namespace PersonalFinance.Application.Tests.Unit.Auth
{
    public class LoginUseCaseTests
    {
        private readonly Mock<IUserRepository> _userRepo = new();
        private readonly Mock<IUserRoleRepository> _roleRepo = new();
        private readonly Mock<IPasswordHasher> _hasher = new();
        private readonly Mock<ITokenService> _tokenSvc = new();
        private readonly LoginWithRolesUseCase _sut;

        public LoginUseCaseTests()
        {
            _sut = new LoginWithRolesUseCase(
                _userRepo.Object, _roleRepo.Object,
                _hasher.Object, _tokenSvc.Object);
        }

        private static User FakeUser() =>
            User.Create("Caique", "caique@monkeybomb.com", "hashed_password");

        // ── Sucesso ───────────────────────────────────────────────────────────────

        [Fact(DisplayName = "Deve retornar token para credenciais válidas")]
        public async Task Execute_WithValidCredentials_ShouldReturnToken()
        {
            var user = FakeUser();
            _userRepo.Setup(r => r.GetByEmailAsync("caique@monkeybomb.com", default))
                     .ReturnsAsync(user);
            _hasher.Setup(h => h.Verify("SenhaForte@123", "hashed_password"))
                   .Returns(true);
            _roleRepo.Setup(r => r.GetRoleNamesByUserIdAsync(user.Id, default))
                     .ReturnsAsync(new[] { "User" });
            _tokenSvc.Setup(t => t.Generate(user, It.IsAny<IEnumerable<string>>()))
                     .Returns("jwt_token_string");

            var result = await _sut.ExecuteAsync(
                new LoginDto("caique@monkeybomb.com", "SenhaForte@123"));

            result.Token.Should().Be("jwt_token_string");
            result.Email.Should().Be("caique@monkeybomb.com");
        }

        // ── Usuário não encontrado ────────────────────────────────────────────────

        [Fact(DisplayName = "Deve lançar exceção para e-mail não cadastrado")]
        public async Task Execute_WithUnknownEmail_ShouldThrow()
        {
            _userRepo.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), default))
                     .ReturnsAsync((User?)null);

            var act = () => _sut.ExecuteAsync(
                new LoginDto("unknown@x.com", "Senha@123"));

            await act.Should().ThrowAsync<DomainException>()
                     .WithMessage("*credenciais*");
        }

        // ── Senha incorreta ───────────────────────────────────────────────────────

        [Fact(DisplayName = "Deve lançar exceção para senha incorreta")]
        public async Task Execute_WithWrongPassword_ShouldThrow()
        {
            var user = FakeUser();
            _userRepo.Setup(r => r.GetByEmailAsync("caique@monkeybomb.com", default))
                     .ReturnsAsync(user);
            _hasher.Setup(h => h.Verify("SenhaErrada", "hashed_password"))
                   .Returns(false);

            var act = () => _sut.ExecuteAsync(
                new LoginDto("caique@monkeybomb.com", "SenhaErrada"));

            await act.Should().ThrowAsync<DomainException>()
                     .WithMessage("*credenciais*");
        }

        // ── Usuário inativo ───────────────────────────────────────────────────────

        [Fact(DisplayName = "Deve lançar exceção para usuário inativo (soft deleted)")]
        public async Task Execute_WithInactiveUser_ShouldThrow()
        {
            var user = FakeUser();
            user.SoftDelete();

            _userRepo.Setup(r => r.GetByEmailAsync("caique@monkeybomb.com", default))
                     .ReturnsAsync(user);

            var act = () => _sut.ExecuteAsync(
                new LoginDto("caique@monkeybomb.com", "SenhaForte@123"));

            await act.Should().ThrowAsync<DomainException>()
                     .WithMessage("*inativo*");
        }

        // ── Token não deve ser gerado antes da verificação da senha ───────────────

        [Fact(DisplayName = "Não deve gerar token se senha for inválida")]
        public async Task Execute_WithWrongPassword_ShouldNotGenerateToken()
        {
            var user = FakeUser();
            _userRepo.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), default))
                     .ReturnsAsync(user);
            _hasher.Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>()))
                   .Returns(false);

            try { await _sut.ExecuteAsync(new LoginDto("caique@monkeybomb.com", "Errada")); }
            catch { /* esperado */ }

            _tokenSvc.Verify(t => t.Generate(
                It.IsAny<User>(), It.IsAny<IEnumerable<string>>()), Times.Never);
        }

        // ── Token inclui roles ────────────────────────────────────────────────────

        [Fact(DisplayName = "Deve passar as roles do usuário para o token")]
        public async Task Execute_ShouldPassRolesToToken()
        {
            var user = FakeUser();
            var roles = new[] { "Admin", "User" };

            _userRepo.Setup(r => r.GetByEmailAsync("caique@monkeybomb.com", default))
                     .ReturnsAsync(user);
            _hasher.Setup(h => h.Verify("SenhaForte@123", "hashed_password"))
                   .Returns(true);
            _roleRepo.Setup(r => r.GetRoleNamesByUserIdAsync(user.Id, default))
                     .ReturnsAsync(roles);
            _tokenSvc.Setup(t => t.Generate(user, roles))
                     .Returns("admin_token");

            var result = await _sut.ExecuteAsync(
                new LoginDto("caique@monkeybomb.com", "SenhaForte@123"));

            result.Token.Should().Be("admin_token");
            _tokenSvc.Verify(t => t.Generate(user, roles), Times.Once);
        }
    }
}
