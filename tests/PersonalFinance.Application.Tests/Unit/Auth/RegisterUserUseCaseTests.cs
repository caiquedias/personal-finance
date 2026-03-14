using FluentAssertions;
using Moq;
using PersonalFinance.Application.DTOs.Auth;
using PersonalFinance.Application.UseCases.Auth;
using PersonalFinance.Domain.Entities.Auth;
using PersonalFinance.Domain.Exceptions;
using PersonalFinance.Domain.Interfaces.Repositories;
using PersonalFinance.Domain.Interfaces.Services;
using Xunit;

namespace PersonalFinance.Application.Tests.Unit.Auth
{
    public class RegisterUserUseCaseTests
    {
        private readonly Mock<IUserRepository> _userRepo = new();
        private readonly Mock<IPasswordHasher> _hasher = new();
        private readonly Mock<IUnitOfWork> _uow = new();
        private readonly RegisterUserUseCase _sut;

        public RegisterUserUseCaseTests()
        {
            _sut = new RegisterUserUseCase(_userRepo.Object, _hasher.Object, _uow.Object);
        }

        private static RegisterUserDto ValidDto() => new(
            Name: "Caique Dias",
            Email: "caique@monkeybomb.com",
            Password: "SenhaForte@123"
        );

        // ── Sucesso ───────────────────────────────────────────────────────────────

        [Fact(DisplayName = "Deve registrar usuário com dados válidos")]
        public async Task Execute_WithValidData_ShouldCreateUser()
        {
            _userRepo.Setup(r => r.ExistsByEmailAsync(It.IsAny<string>(), default))
                     .ReturnsAsync(false);
            _hasher.Setup(h => h.Hash(It.IsAny<string>()))
                   .Returns("argon2_hash");

            var result = await _sut.ExecuteAsync(ValidDto());

            result.Should().NotBeNull();
            result.Email.Should().Be("caique@monkeybomb.com");
            _userRepo.Verify(r => r.AddAsync(It.IsAny<User>(), default), Times.Once);
            _uow.Verify(u => u.CommitAsync(default), Times.Once);
        }

        // ── E-mail duplicado ──────────────────────────────────────────────────────

        [Fact(DisplayName = "Deve lançar exceção se e-mail já estiver em uso")]
        public async Task Execute_WithDuplicateEmail_ShouldThrow()
        {
            _userRepo.Setup(r => r.ExistsByEmailAsync("caique@monkeybomb.com", default))
                     .ReturnsAsync(true);

            var act = () => _sut.ExecuteAsync(ValidDto());

            await act.Should().ThrowAsync<DomainException>()
                     .WithMessage("*e-mail*");

            _uow.Verify(u => u.CommitAsync(default), Times.Never);
        }

        // ── Validação de input ────────────────────────────────────────────────────

        [Theory(DisplayName = "Deve lançar exceção para campos obrigatórios ausentes")]
        [MemberData(nameof(InvalidDtos))]
        public async Task Execute_WithInvalidDto_ShouldThrow(RegisterUserDto dto)
        {
            var act = () => _sut.ExecuteAsync(dto);
            await act.Should().ThrowAsync<Exception>();
        }

        public static IEnumerable<object[]> InvalidDtos() =>
        [
            [new RegisterUserDto("",       "caique@monkeybomb.com", "Senha@123")],
            [new RegisterUserDto("Caique", "",                      "Senha@123")],
            [new RegisterUserDto("Caique", "not_an_email",          "Senha@123")],
            [new RegisterUserDto("Caique", "caique@monkeybomb.com", "")],
    ];

        // ── Hash ──────────────────────────────────────────────────────────────────

        [Fact(DisplayName = "Deve chamar o hasher antes de persistir")]
        public async Task Execute_ShouldHashPasswordBeforePersisting()
        {
            _userRepo.Setup(r => r.ExistsByEmailAsync(It.IsAny<string>(), default))
                     .ReturnsAsync(false);
            _hasher.Setup(h => h.Hash("SenhaForte@123")).Returns("hash_result");

            await _sut.ExecuteAsync(ValidDto());

            _hasher.Verify(h => h.Hash("SenhaForte@123"), Times.Once);
            _userRepo.Verify(r => r.AddAsync(
                It.Is<User>(u => u.PasswordHash == "hash_result"), default), Times.Once);
        }
    }
}
