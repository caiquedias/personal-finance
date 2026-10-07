using FluentAssertions;
using FluentValidation;
using Moq;
using PersonalFinance.Application.DTOs.Auth;
using PersonalFinance.Application.Interfaces;
using PersonalFinance.Application.Options;
using PersonalFinance.Application.Tests.Unit.Support;
using PersonalFinance.Application.UseCases.Auth;
using PersonalFinance.Domain.Entities.Auth;
using PersonalFinance.Domain.Enums;
using PersonalFinance.Domain.Exceptions;
using PersonalFinance.Domain.Interfaces.Repositories;
using Xunit;

namespace PersonalFinance.Application.Tests.Unit.Auth;

/// <summary>
/// ConfirmEmailUseCase (#404): confirma o e-mail com e-mail + código de 6 dígitos. Mesmas regras de
/// tentativas/TTL/uso único do reset e a mesma resposta genérica para qualquer falha.
/// </summary>
public class ConfirmEmailUseCaseTests
{
    private const string InvalidMessage = "Código inválido ou expirado.";
    private const string GoodCode = "654321";
    private const string StoredHash = "stored-hash";

    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<IUserTokenRepository> _tokenRepo = new();
    private readonly Mock<IOneTimeCodeService> _codes = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly UserTokenOptions _options = new() { HmacKey = "k", MaxAttempts = 3, CodeTtlMinutes = 10, ResendCooldownSeconds = 60 };

    private ConfirmEmailUseCase Sut(IValidator<ConfirmEmailDto>? validator = null) =>
        UseCaseFactory.Create<ConfirmEmailUseCase>(
            _userRepo.Object, _tokenRepo.Object, _codes.Object, _uow.Object, _options,
            validator ?? TestValidators.Valid<ConfirmEmailDto>());

    private static ConfirmEmailDto Dto(string code = GoodCode) => new("ana@x.com", code);

    private (User user, UserToken token) Arrange(TimeSpan? tokenAge = null)
    {
        var user = User.Create("Ana", "ana@x.com", "hash");
        var token = UserToken.Create(user.Id, UserTokenPurpose.EmailVerification,
            DateTime.UtcNow - (tokenAge ?? TimeSpan.FromMinutes(1)), TimeSpan.FromMinutes(10));
        token.SetTokenHash(StoredHash);

        _userRepo.Setup(r => r.GetByEmailAsync("ana@x.com", It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _tokenRepo.Setup(r => r.GetLatestAsync(user.Id, UserTokenPurpose.EmailVerification, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(token);
        _codes.Setup(c => c.Verify(token.Id, user.Id, UserTokenPurpose.EmailVerification, GoodCode, StoredHash)).Returns(true);
        return (user, token);
    }

    [Fact(DisplayName = "Sucesso: confirma o e-mail, marca o token como usado e commita uma vez")]
    public async Task Execute_Success_ShouldConfirmEmailAndMarkTokenUsed()
    {
        var (user, token) = Arrange();

        await Sut().ExecuteAsync(Dto());

        user.IsEmailConfirmed.Should().BeTrue();
        user.EmailConfirmedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(30));
        token.UsedAt.Should().NotBeNull();
        _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "Sucesso não altera senha, SecurityStamp nem MFA")]
    public async Task Execute_Success_ShouldNotTouchCredentials()
    {
        var (user, _) = Arrange();
        var stamp = user.SecurityStamp;

        await Sut().ExecuteAsync(Dto());

        user.PasswordHash.Should().Be("hash");
        user.SecurityStamp.Should().Be(stamp);
    }

    [Fact(DisplayName = "Busca o token do propósito EmailVerification (nunca o de reset) e normaliza o e-mail")]
    public async Task Execute_ShouldUseEmailVerificationPurposeAndNormalizeEmail()
    {
        var (user, _) = Arrange();

        await Sut().ExecuteAsync(new ConfirmEmailDto("  ANA@X.com ", GoodCode));

        _userRepo.Verify(r => r.GetByEmailAsync("ana@x.com", It.IsAny<CancellationToken>()), Times.Once);
        _tokenRepo.Verify(r => r.GetLatestAsync(user.Id, UserTokenPurpose.PasswordReset, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "Código errado: mensagem genérica, tentativa contada e persistida, e-mail continua não confirmado")]
    public async Task Execute_WrongCode_ShouldRegisterAttempt()
    {
        var (user, token) = Arrange();

        var act = () => Sut().ExecuteAsync(Dto("000000"));

        await act.Should().ThrowAsync<DomainException>().WithMessage(InvalidMessage);
        token.Attempts.Should().Be(1);
        user.IsEmailConfirmed.Should().BeFalse();
        _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "3ª falha invalida o token: o código CERTO depois é recusado")]
    public async Task Execute_ThirdFailure_ShouldInvalidateTokenEvenForCorrectCode()
    {
        var (user, token) = Arrange();
        var sut = Sut();

        for (var i = 0; i < 3; i++)
            await Assert.ThrowsAsync<DomainException>(() => sut.ExecuteAsync(Dto("000000")));

        var act = () => sut.ExecuteAsync(Dto(GoodCode));

        await act.Should().ThrowAsync<DomainException>().WithMessage(InvalidMessage);
        user.IsEmailConfirmed.Should().BeFalse();
        token.UsedAt.Should().BeNull();
    }

    [Fact(DisplayName = "Conflito de concorrência ao contar a tentativa: repete e mantém a resposta genérica")]
    public async Task Execute_ConcurrencyConflictOnAttempt_ShouldRetry()
    {
        Arrange();
        var calls = 0;
        _uow.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>()))
            .Returns(() => ++calls == 1 ? throw new ConcurrencyConflictException() : Task.CompletedTask);

        var act = () => Sut().ExecuteAsync(Dto("000000"));

        await act.Should().ThrowAsync<DomainException>().WithMessage(InvalidMessage);
        _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact(DisplayName = "Token expirado: mesma mensagem, e-mail não confirmado")]
    public async Task Execute_ExpiredToken_ShouldReject()
    {
        var (user, _) = Arrange(tokenAge: TimeSpan.FromMinutes(30));

        var act = () => Sut().ExecuteAsync(Dto());

        await act.Should().ThrowAsync<DomainException>().WithMessage(InvalidMessage);
        user.IsEmailConfirmed.Should().BeFalse();
    }

    [Fact(DisplayName = "Token já usado: mesma mensagem")]
    public async Task Execute_UsedToken_ShouldReject()
    {
        var (_, token) = Arrange();
        token.MarkUsed(DateTime.UtcNow.AddSeconds(-5));

        var act = () => Sut().ExecuteAsync(Dto());

        await act.Should().ThrowAsync<DomainException>().WithMessage(InvalidMessage);
    }

    [Fact(DisplayName = "Sem token de verificação: mesma mensagem")]
    public async Task Execute_NoToken_ShouldReject()
    {
        var user = User.Create("Ana", "ana@x.com", "hash");
        _userRepo.Setup(r => r.GetByEmailAsync("ana@x.com", It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var act = () => Sut().ExecuteAsync(Dto());

        await act.Should().ThrowAsync<DomainException>().WithMessage(InvalidMessage);
    }

    [Fact(DisplayName = "E-mail inexistente: mesma mensagem, sem commit")]
    public async Task Execute_UnknownUser_ShouldReject()
    {
        _userRepo.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        var act = () => Sut().ExecuteAsync(Dto());

        await act.Should().ThrowAsync<DomainException>().WithMessage(InvalidMessage);
        _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "Usuário inativo: mesma mensagem, e-mail não confirmado")]
    public async Task Execute_InactiveUser_ShouldReject()
    {
        var (user, _) = Arrange();
        user.Deactivate();

        var act = () => Sut().ExecuteAsync(Dto());

        await act.Should().ThrowAsync<DomainException>().WithMessage(InvalidMessage);
        user.IsEmailConfirmed.Should().BeFalse();
    }

    // ── C3: equalização aproximada de timing (HMAC dummy) ─────────────────────

    [Theory(DisplayName = "Usuário inexistente/inativo/removido: executa HMAC dummy (ComputeHash + Verify), mesma mensagem e nada persistido")]
    [InlineData("unknown")]
    [InlineData("inactive")]
    [InlineData("deleted")]
    public async Task Execute_UnusableUser_ShouldRunDummyHmacAndPersistNothing(string scenario)
    {
        if (scenario == "unknown")
        {
            _userRepo.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);
        }
        else
        {
            var (user, _) = Arrange();
            if (scenario == "inactive") user.Deactivate(); else user.SoftDelete();
        }

        var act = () => Sut().ExecuteAsync(Dto());

        await act.Should().ThrowAsync<DomainException>().WithMessage(InvalidMessage);
        _codes.Verify(c => c.ComputeHash(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<UserTokenPurpose>(), It.IsAny<string>()), Times.Once);
        _codes.Verify(c => c.Verify(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<UserTokenPurpose>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        _userRepo.Verify(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        _tokenRepo.Verify(r => r.UpdateAsync(It.IsAny<UserToken>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "Usuário utilizável com código errado: não faz HMAC dummy extra (só a verificação real)")]
    public async Task Execute_UsableUserWrongCode_ShouldNotRunExtraDummy()
    {
        Arrange();

        var act = () => Sut().ExecuteAsync(Dto("000000"));

        await act.Should().ThrowAsync<DomainException>().WithMessage(InvalidMessage);
        _codes.Verify(c => c.Verify(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<UserTokenPurpose>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        _codes.Verify(c => c.ComputeHash(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<UserTokenPurpose>(), It.IsAny<string>()), Times.Never);
    }

    [Fact(DisplayName = "Validator reprovado: ValidationException sem tocar repositórios nem commit")]
    public async Task Execute_WhenValidatorFails_ShouldThrowAndTouchNothing()
    {
        var sut = Sut(TestValidators.Invalid<ConfirmEmailDto>());

        await Assert.ThrowsAsync<ValidationException>(() => sut.ExecuteAsync(Dto()));

        _userRepo.Invocations.Should().BeEmpty();
        _tokenRepo.Invocations.Should().BeEmpty();
        _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
