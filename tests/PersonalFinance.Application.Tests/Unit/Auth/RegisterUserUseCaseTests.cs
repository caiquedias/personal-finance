using FluentAssertions;
using Moq;
using PersonalFinance.Application.DTOs.Auth;
using PersonalFinance.Application.UseCases.Auth;
using PersonalFinance.Domain.Entities.Auth;
using PersonalFinance.Domain.Enums;
using PersonalFinance.Domain.Interfaces.Repositories;
using PersonalFinance.Domain.Interfaces.Services;
using FluentValidation;
using PersonalFinance.Application.Tests.Unit.Support;
using Xunit;

namespace PersonalFinance.Application.Tests.Unit.Auth
{
    /// <summary>
    /// RegisterUserUseCase (#404): resposta genérica sempre (sem enumeração de e-mail), Hash também no
    /// duplicado (equaliza o tempo), emissão do código de verificação de e-mail só para conta nova.
    /// </summary>
    public class RegisterUserUseCaseTests
    {
        private readonly Mock<IUserRepository> _userRepo = new();
        private readonly Mock<IPasswordHasher> _hasher = new();
        private readonly Mock<IUnitOfWork> _uow = new();
        private readonly IssuerHarness _h;
        private readonly RegisterUserUseCase _sut;

        public RegisterUserUseCaseTests()
        {
            _h = new IssuerHarness(_uow);
            _sut = Build(TestValidators.Valid<RegisterUserDto>());
        }

        private RegisterUserUseCase Build(IValidator<RegisterUserDto> validator) =>
            UseCaseFactory.Create<RegisterUserUseCase>(
                _userRepo.Object, _hasher.Object, _uow.Object, validator, _h.Issuer);

        private static RegisterUserDto ValidDto() => new(
            Name: "Caique Dias",
            Email: "caique@monkeybomb.com",
            Password: "SenhaForte@123"
        );

        // ── Sucesso ───────────────────────────────────────────────────────────────

        [Fact(DisplayName = "Deve registrar usuário com dados válidos e persistir uma única vez")]
        public async Task Execute_WithValidData_ShouldCreateUser()
        {
            _userRepo.Setup(r => r.ExistsByEmailAsync(It.IsAny<string>(), default))
                     .ReturnsAsync(false);
            _hasher.Setup(h => h.Hash(It.IsAny<string>()))
                   .Returns("argon2_hash");

            await _sut.ExecuteAsync(ValidDto());

            _userRepo.Verify(r => r.AddAsync(
                It.Is<User>(u => u.Email == "caique@monkeybomb.com" && u.Name == "Caique Dias"), default), Times.Once);
            _uow.Verify(u => u.CommitAsync(default), Times.Once);
        }

        [Fact(DisplayName = "Conta nova: emite o código de verificação de e-mail e enfileira o e-mail")]
        public async Task Execute_NewUser_ShouldIssueEmailVerification()
        {
            _userRepo.Setup(r => r.ExistsByEmailAsync(It.IsAny<string>(), default)).ReturnsAsync(false);
            _hasher.Setup(h => h.Hash(It.IsAny<string>())).Returns("argon2_hash");
            User? added = null;
            _userRepo.Setup(r => r.AddAsync(It.IsAny<User>(), default))
                     .Callback<User, CancellationToken>((u, _) => added = u)
                     .Returns(Task.CompletedTask);

            await _sut.ExecuteAsync(ValidDto());

            var token = _h.Added.Should().ContainSingle().Subject;
            token.Purpose.Should().Be(UserTokenPurpose.EmailVerification);
            token.UserId.Should().Be(added!.Id);
            var message = _h.Enqueued.Should().ContainSingle().Subject;
            message.To.Should().Be("caique@monkeybomb.com");
            message.HtmlBody.Should().Contain(IssuerHarness.Code).And.Contain("/confirm-email#email=");
        }

        // ── E-mail duplicado: resposta genérica, sem efeito ───────────────────────

        [Fact(DisplayName = "E-mail duplicado: não lança (resposta idêntica à do sucesso)")]
        public async Task Execute_WithDuplicateEmail_ShouldNotThrow()
        {
            _userRepo.Setup(r => r.ExistsByEmailAsync("caique@monkeybomb.com", default))
                     .ReturnsAsync(true);

            var act = () => _sut.ExecuteAsync(ValidDto());

            await act.Should().NotThrowAsync();
        }

        [Fact(DisplayName = "E-mail duplicado: não cria usuário, não commita e não enfileira e-mail")]
        public async Task Execute_WithDuplicateEmail_ShouldHaveNoSideEffects()
        {
            _userRepo.Setup(r => r.ExistsByEmailAsync("caique@monkeybomb.com", default))
                     .ReturnsAsync(true);

            await _sut.ExecuteAsync(ValidDto());

            _userRepo.Verify(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
            _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
            _h.Enqueued.Should().BeEmpty();
            _h.Added.Should().BeEmpty();
        }

        [Fact(DisplayName = "E-mail duplicado: ainda executa Hash da senha (equaliza o tempo de resposta)")]
        public async Task Execute_WithDuplicateEmail_ShouldStillHashPassword()
        {
            _userRepo.Setup(r => r.ExistsByEmailAsync("caique@monkeybomb.com", default))
                     .ReturnsAsync(true);

            await _sut.ExecuteAsync(ValidDto());

            _hasher.Verify(h => h.Hash("SenhaForte@123"), Times.Once);
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

        // ── Validação (#396) ──────────────────────────────────────────────────

        [Fact(DisplayName = "Deve lançar ValidationException quando o validator reprova o DTO")]
        public async Task Execute_WhenValidatorFails_ShouldThrowValidationException()
        {
            var sut = Build(TestValidators.Invalid<RegisterUserDto>("Senha curta."));

            var act = () => sut.ExecuteAsync(ValidDto());

            var ex = await Assert.ThrowsAsync<ValidationException>(act);
            Assert.Contains("Senha curta.", ex.Message);
        }

        [Fact(DisplayName = "Não deve acessar repositório, fila nem persistir quando o validator reprova")]
        public async Task Execute_WhenValidatorFails_ShouldNotTouchRepositoryOrCommit()
        {
            var sut = Build(TestValidators.Invalid<RegisterUserDto>());

            await Assert.ThrowsAsync<ValidationException>(() => sut.ExecuteAsync(ValidDto()));

            _userRepo.Verify(r => r.ExistsByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
            _userRepo.Verify(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
            _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
            _h.Enqueued.Should().BeEmpty();
        }
    }
}
