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
using PersonalFinance.Domain.Interfaces.Services;
using Xunit;

namespace PersonalFinance.Application.Tests.Unit.Auth;

/// <summary>
/// CompletePasswordResetUseCase (#404): efeitos do reset concluído, máx. de tentativas, TTL, uso único,
/// resposta idêntica para qualquer falha (anti-enumeração) e retry de concorrência no contador.
/// </summary>
public class CompletePasswordResetUseCaseTests
{
    private const string Ip = "203.0.113.7";
    private const string InvalidMessage = "Código inválido ou expirado.";
    private const string GoodCode = "123456";
    private const string StoredHash = "stored-hash";

    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<IUserTokenRepository> _tokenRepo = new();
    private readonly Mock<IOneTimeCodeService> _codes = new();
    private readonly Mock<IPasswordHasher> _hasher = new();
    private readonly Mock<IAuditLogRepository> _audit = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly UserTokenOptions _options = new() { HmacKey = "k", MaxAttempts = 3, CodeTtlMinutes = 10, ResendCooldownSeconds = 60 };
    private readonly AuditTrace _trace;

    public CompletePasswordResetUseCaseTests()
    {
        _trace = AuditTrace.Track(_audit, _uow);
        _hasher.Setup(h => h.Hash("NovaSenha@456")).Returns("new-hash");
    }

    private CompletePasswordResetUseCase Sut(IValidator<CompletePasswordResetDto>? validator = null) =>
        UseCaseFactory.Create<CompletePasswordResetUseCase>(
            _userRepo.Object, _tokenRepo.Object, _codes.Object, _hasher.Object,
            _audit.Object, _uow.Object, _options,
            validator ?? TestValidators.Valid<CompletePasswordResetDto>());

    private static CompletePasswordResetDto Dto(string code = GoodCode) =>
        new("ana@x.com", code, "NovaSenha@456");

    /// <summary>Usuário ativo + token utilizável do propósito PasswordReset; só GoodCode verifica.</summary>
    private (User user, UserToken token) Arrange(TimeSpan? tokenAge = null)
    {
        var user = User.Create("Ana", "ana@x.com", "old-hash");
        var token = UserToken.Create(user.Id, UserTokenPurpose.PasswordReset,
            DateTime.UtcNow - (tokenAge ?? TimeSpan.FromMinutes(1)), TimeSpan.FromMinutes(10));
        token.SetTokenHash(StoredHash);

        _userRepo.Setup(r => r.GetByEmailAsync("ana@x.com", It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _tokenRepo.Setup(r => r.GetLatestAsync(user.Id, UserTokenPurpose.PasswordReset, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(token);
        _codes.Setup(c => c.Verify(token.Id, user.Id, UserTokenPurpose.PasswordReset, GoodCode, StoredHash)).Returns(true);
        return (user, token);
    }

    // ── Sucesso ───────────────────────────────────────────────────────────────

    [Fact(DisplayName = "Sucesso: troca o hash da senha, rotaciona SecurityStamp e zera falhas/bloqueio")]
    public async Task Execute_Success_ShouldChangePasswordRotateStampAndResetLockout()
    {
        var (user, _) = Arrange();
        user.RegisterFailedLogin(3, TimeSpan.FromMinutes(15), DateTime.UtcNow);
        user.RegisterFailedLogin(3, TimeSpan.FromMinutes(15), DateTime.UtcNow);
        user.RegisterFailedLogin(3, TimeSpan.FromMinutes(15), DateTime.UtcNow);
        user.LockedUntil.Should().NotBeNull();
        var stamp = user.SecurityStamp;

        await Sut().ExecuteAsync(Dto(), Ip);

        user.PasswordHash.Should().Be("new-hash");
        user.SecurityStamp.Should().NotBe(stamp);
        user.FailedLoginCount.Should().Be(0);
        user.LockedUntil.Should().BeNull();
        _hasher.Verify(h => h.Hash("NovaSenha@456"), Times.Once);
    }

    [Fact(DisplayName = "Sucesso: confirma o e-mail e marca o token como usado")]
    public async Task Execute_Success_ShouldConfirmEmailAndMarkTokenUsed()
    {
        var (user, token) = Arrange();

        await Sut().ExecuteAsync(Dto(), Ip);

        user.IsEmailConfirmed.Should().BeTrue();
        token.UsedAt.Should().NotBeNull();
        token.IsUsable(DateTime.UtcNow).Should().BeFalse();
    }

    [Fact(DisplayName = "Sucesso: MFA continua ativo")]
    public async Task Execute_Success_ShouldKeepMfaEnabled()
    {
        var (user, _) = Arrange();
        user.SetPendingMfaSecret("cipher");
        user.EnableMfa(DateTime.UtcNow);

        await Sut().ExecuteAsync(Dto(), Ip);

        user.MfaEnabled.Should().BeTrue();
        user.MfaSecretEncrypted.Should().Be("cipher");
    }

    [Fact(DisplayName = "Sucesso: audita PasswordResetCompleted (ator = alvo) com IP, antes do commit único")]
    public async Task Execute_Success_ShouldAuditCompletedInSameTransaction()
    {
        var (user, _) = Arrange();

        await Sut().ExecuteAsync(Dto(), Ip);

        var log = _trace.Logs.Should().ContainSingle().Subject;
        log.Action.Should().Be(AuditAction.PasswordResetCompleted);
        log.ActorUserId.Should().Be(user.Id);
        log.TargetUserId.Should().Be(user.Id);
        log.IpAddress.Should().Be(Ip);
        (log.Details ?? string.Empty).Should().NotContain(GoodCode).And.NotContain("NovaSenha").And.NotContain("new-hash");
        _trace.Order.Should().Equal("audit", "commit");
    }

    [Fact(DisplayName = "Verifica o código via IOneTimeCodeService com Id do token, usuário, propósito e hash guardado")]
    public async Task Execute_ShouldVerifyCodeAgainstStoredHash()
    {
        var (user, token) = Arrange();

        await Sut().ExecuteAsync(Dto(), Ip);

        _codes.Verify(c => c.Verify(token.Id, user.Id, UserTokenPurpose.PasswordReset, GoodCode, StoredHash), Times.Once);
        _tokenRepo.Verify(r => r.GetLatestAsync(user.Id, UserTokenPurpose.PasswordReset, It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        _tokenRepo.Verify(r => r.GetLatestAsync(user.Id, UserTokenPurpose.EmailVerification, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "E-mail é normalizado (trim + lowercase) antes da busca")]
    public async Task Execute_ShouldNormalizeEmail()
    {
        Arrange();

        await Sut().ExecuteAsync(new CompletePasswordResetDto("  ANA@X.com ", GoodCode, "NovaSenha@456"), Ip);

        _userRepo.Verify(r => r.GetByEmailAsync("ana@x.com", It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Código errado / tentativas ────────────────────────────────────────────

    [Fact(DisplayName = "Código errado: lança a mensagem genérica, conta a tentativa, persiste e não troca a senha")]
    public async Task Execute_WrongCode_ShouldRegisterAttemptAndKeepPassword()
    {
        var (user, token) = Arrange();

        var act = () => Sut().ExecuteAsync(Dto("000000"), Ip);

        await act.Should().ThrowAsync<DomainException>().WithMessage(InvalidMessage);
        token.Attempts.Should().Be(1);
        token.IsUsable(DateTime.UtcNow).Should().BeTrue();
        user.PasswordHash.Should().Be("old-hash");
        user.IsEmailConfirmed.Should().BeFalse();
        _hasher.Verify(h => h.Hash(It.IsAny<string>()), Times.Never);
        _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        _trace.Logs.Should().BeEmpty();
    }

    [Fact(DisplayName = "3ª falha invalida o token: o código CERTO depois é recusado")]
    public async Task Execute_ThirdFailure_ShouldInvalidateTokenEvenForCorrectCode()
    {
        var (user, token) = Arrange();
        var sut = Sut();

        for (var i = 0; i < 3; i++)
            await Assert.ThrowsAsync<DomainException>(() => sut.ExecuteAsync(Dto("000000"), Ip));
        token.IsUsable(DateTime.UtcNow).Should().BeFalse();

        var act = () => sut.ExecuteAsync(Dto(GoodCode), Ip);

        await act.Should().ThrowAsync<DomainException>().WithMessage(InvalidMessage);
        user.PasswordHash.Should().Be("old-hash");
        token.UsedAt.Should().BeNull();
        _trace.Logs.Should().BeEmpty();
    }

    [Fact(DisplayName = "MaxAttempts configurável: com 1, a primeira falha já invalida")]
    public async Task Execute_MaxAttemptsOne_ShouldInvalidateOnFirstFailure()
    {
        _options.MaxAttempts = 1;
        var (_, token) = Arrange();

        await Assert.ThrowsAsync<DomainException>(() => Sut().ExecuteAsync(Dto("000000"), Ip));

        token.IsUsable(DateTime.UtcNow).Should().BeFalse();
    }

    [Fact(DisplayName = "Conflito de concorrência ao contar a tentativa: recarrega e repete; a resposta continua genérica")]
    public async Task Execute_ConcurrencyConflictOnAttempt_ShouldRetry()
    {
        var (_, _) = Arrange();
        var calls = 0;
        _uow.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>()))
            .Returns(() => ++calls == 1 ? throw new ConcurrencyConflictException() : Task.CompletedTask);

        var act = () => Sut().ExecuteAsync(Dto("000000"), Ip);

        await act.Should().ThrowAsync<DomainException>().WithMessage(InvalidMessage);
        _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact(DisplayName = "Conflitos de concorrência esgotados: ainda responde com a mensagem genérica (nunca 409/500)")]
    public async Task Execute_ConcurrencyConflictsExhausted_ShouldStillThrowGenericMessage()
    {
        Arrange();
        _uow.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new ConcurrencyConflictException());

        var act = () => Sut().ExecuteAsync(Dto("000000"), Ip);

        await act.Should().ThrowAsync<DomainException>().WithMessage(InvalidMessage);
    }

    // ── Token inválido: expirado / usado / inexistente ────────────────────────

    [Fact(DisplayName = "Token expirado (mesmo com código certo): mesma mensagem, sem trocar senha")]
    public async Task Execute_ExpiredToken_ShouldRejectWithGenericMessage()
    {
        var (user, _) = Arrange(tokenAge: TimeSpan.FromMinutes(30));

        var act = () => Sut().ExecuteAsync(Dto(), Ip);

        await act.Should().ThrowAsync<DomainException>().WithMessage(InvalidMessage);
        user.PasswordHash.Should().Be("old-hash");
        _trace.Logs.Should().BeEmpty();
    }

    [Fact(DisplayName = "Token já usado (reuso): mesma mensagem, sem trocar senha")]
    public async Task Execute_UsedToken_ShouldRejectWithGenericMessage()
    {
        var (user, token) = Arrange();
        token.MarkUsed(DateTime.UtcNow.AddSeconds(-5));

        var act = () => Sut().ExecuteAsync(Dto(), Ip);

        await act.Should().ThrowAsync<DomainException>().WithMessage(InvalidMessage);
        user.PasswordHash.Should().Be("old-hash");
    }

    [Fact(DisplayName = "Segundo uso do mesmo código após sucesso é recusado")]
    public async Task Execute_ReusingCodeAfterSuccess_ShouldBeRejected()
    {
        var (_, _) = Arrange();
        var sut = Sut();
        await sut.ExecuteAsync(Dto(), Ip);

        var act = () => sut.ExecuteAsync(Dto(), Ip);

        await act.Should().ThrowAsync<DomainException>().WithMessage(InvalidMessage);
        _hasher.Verify(h => h.Hash(It.IsAny<string>()), Times.Once);
    }

    [Fact(DisplayName = "Sem token para o usuário (ou só de outro propósito): mesma mensagem")]
    public async Task Execute_NoToken_ShouldRejectWithGenericMessage()
    {
        var user = User.Create("Ana", "ana@x.com", "old-hash");
        _userRepo.Setup(r => r.GetByEmailAsync("ana@x.com", It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _tokenRepo.Setup(r => r.GetLatestAsync(user.Id, UserTokenPurpose.PasswordReset, It.IsAny<CancellationToken>()))
                  .ReturnsAsync((UserToken?)null);

        var act = () => Sut().ExecuteAsync(Dto(), Ip);

        await act.Should().ThrowAsync<DomainException>().WithMessage(InvalidMessage);
        _tokenRepo.Verify(r => r.GetLatestAsync(user.Id, UserTokenPurpose.EmailVerification, It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Anti-enumeração: usuário inexistente / inativo ────────────────────────

    [Fact(DisplayName = "E-mail inexistente: mesma mensagem genérica, sem hash de senha nem auditoria")]
    public async Task Execute_UnknownUser_ShouldRejectWithGenericMessage()
    {
        _userRepo.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        var act = () => Sut().ExecuteAsync(Dto(), Ip);

        await act.Should().ThrowAsync<DomainException>().WithMessage(InvalidMessage);
        _hasher.Verify(h => h.Hash(It.IsAny<string>()), Times.Never);
        _trace.Logs.Should().BeEmpty();
        _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "Usuário inativo: mesma mensagem genérica e senha intacta")]
    public async Task Execute_InactiveUser_ShouldRejectWithGenericMessage()
    {
        var (user, _) = Arrange();
        user.Deactivate();

        var act = () => Sut().ExecuteAsync(Dto(), Ip);

        await act.Should().ThrowAsync<DomainException>().WithMessage(InvalidMessage);
        user.PasswordHash.Should().Be("old-hash");
    }

    [Fact(DisplayName = "Usuário removido: mesma mensagem genérica e senha intacta")]
    public async Task Execute_DeletedUser_ShouldRejectWithGenericMessage()
    {
        var (user, _) = Arrange();
        user.SoftDelete();

        var act = () => Sut().ExecuteAsync(Dto(), Ip);

        await act.Should().ThrowAsync<DomainException>().WithMessage(InvalidMessage);
        user.PasswordHash.Should().Be("old-hash");
    }

    // ── Validação ─────────────────────────────────────────────────────────────

    [Fact(DisplayName = "Validator reprovado: ValidationException sem tocar repositórios nem commit")]
    public async Task Execute_WhenValidatorFails_ShouldThrowAndTouchNothing()
    {
        var sut = Sut(TestValidators.Invalid<CompletePasswordResetDto>());

        await Assert.ThrowsAsync<ValidationException>(() => sut.ExecuteAsync(Dto(), Ip));

        _userRepo.Invocations.Should().BeEmpty();
        _tokenRepo.Invocations.Should().BeEmpty();
        _hasher.Invocations.Should().BeEmpty();
        _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
