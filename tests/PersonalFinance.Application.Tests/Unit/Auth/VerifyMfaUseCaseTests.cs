using FluentAssertions;
using Moq;
using PersonalFinance.Application.DTOs.Auth;
using PersonalFinance.Application.Options;
using PersonalFinance.Application.UseCases.Auth;
using PersonalFinance.Domain.Entities.Auth;
using PersonalFinance.Domain.Exceptions;
using PersonalFinance.Domain.Interfaces.Repositories;
using PersonalFinance.Domain.Interfaces.Services;
using Xunit;

namespace PersonalFinance.Application.Tests.Unit.Auth;

/// <summary>
/// VerifyMfaUseCase (#393): 2ª etapa do login. TOTP com anti-replay OU recovery code de uso único.
/// Falhas contam no lockout global (User) e por par (conta, IP) — mesmas regras da #391.
/// Sucesso emite o token completo e zera os contadores.
/// </summary>
public class VerifyMfaUseCaseTests
{
    private const string Ip = "1.1.1.1";
    private const string PlainSecret = "JBSWY3DPEHPK3PXPJBSWY3DPEHPK3PXP";
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(15);

    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<IUserRoleRepository> _roleRepo = new();
    private readonly Mock<ILoginThrottleRepository> _throttleRepo = new();
    private readonly Mock<IMfaRecoveryCodeRepository> _recoveryRepo = new();
    private readonly Mock<ITotpService> _totp = new();
    private readonly Mock<ISecretProtector> _protector = new();
    private readonly Mock<IPasswordHasher> _hasher = new();
    private readonly Mock<ITokenService> _tokenSvc = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly LoginLockoutOptions _lockout = new();

    public VerifyMfaUseCaseTests()
    {
        _protector.Setup(p => p.Unprotect("encrypted-blob")).Returns(PlainSecret);
        _totp.Setup(t => t.ValidateCode(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>()))
             .Returns((long?)null);
        _hasher.Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>()))
               .Returns((string plain, string hash) => hash == "h:" + plain);
        _roleRepo.Setup(r => r.GetRoleNamesByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new[] { "User" });
        _tokenSvc.Setup(t => t.Generate(It.IsAny<User>(), It.IsAny<IEnumerable<string>>())).Returns("full-jwt");
        _recoveryRepo.Setup(r => r.GetActiveByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(new List<MfaRecoveryCode>());
    }

    private VerifyMfaUseCase NewSut(LoginLockoutOptions? options = null) => new(
        _userRepo.Object, _roleRepo.Object, _throttleRepo.Object, _recoveryRepo.Object,
        _totp.Object, _protector.Object, _hasher.Object, _tokenSvc.Object,
        _uow.Object, options ?? _lockout);

    private User MfaUser()
    {
        var user = User.Create("Caique", "caique@monkeybomb.com", "hashed_password");
        user.SetPendingMfaSecret("encrypted-blob");
        user.EnableMfa(DateTime.UtcNow);
        _userRepo.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        return user;
    }

    private void SetupValidTotp(string code, long step) =>
        _totp.Setup(t => t.ValidateCode(PlainSecret, code, It.IsAny<DateTime>())).Returns(step);

    private static VerifyMfaDto Code(string code) => new(code);

    // ── Sucesso por TOTP ──────────────────────────────────────────────────────

    [Fact(DisplayName = "TOTP válido deve emitir o token completo com roles e dados do usuário")]
    public async Task Execute_WithValidTotp_ShouldIssueFullToken()
    {
        var user = MfaUser();
        SetupValidTotp("123456", 500);

        var result = await NewSut().ExecuteAsync(user.Id, Code("123456"), Ip);

        result.Token.Should().Be("full-jwt");
        result.MfaRequired.Should().BeFalse();
        result.MfaToken.Should().BeNull();
        result.Email.Should().Be(user.Email);
        result.Name.Should().Be(user.Name);
        _tokenSvc.Verify(t => t.Generate(user, It.Is<IEnumerable<string>>(r => r.Contains("User"))), Times.Once);
    }

    [Fact(DisplayName = "TOTP válido deve registrar o step usado e commitar")]
    public async Task Execute_WithValidTotp_ShouldStoreStepAndCommit()
    {
        var user = MfaUser();
        SetupValidTotp("123456", 500);

        await NewSut().ExecuteAsync(user.Id, Code("123456"), Ip);

        user.LastUsedTotpStep.Should().Be(500);
        _userRepo.Verify(r => r.UpdateAsync(user, It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "Sucesso deve zerar o lockout global e remover o throttle do par (conta, IP)")]
    public async Task Execute_WithValidTotp_ShouldResetCounters()
    {
        var user = MfaUser();
        user.RegisterFailedLogin(50, Window, DateTime.UtcNow);
        user.RegisterFailedLogin(50, Window, DateTime.UtcNow);
        var throttle = LoginThrottle.Create(user.Id, Ip, DateTime.UtcNow);
        throttle.RegisterFailure(5, Window, DateTime.UtcNow);
        _throttleRepo.Setup(r => r.GetAsync(user.Id, Ip, It.IsAny<CancellationToken>())).ReturnsAsync(throttle);
        SetupValidTotp("123456", 500);

        await NewSut().ExecuteAsync(user.Id, Code("123456"), Ip);

        user.FailedLoginCount.Should().Be(0);
        user.LockedUntil.Should().BeNull();
        _throttleRepo.Verify(r => r.RemoveAsync(throttle, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "TOTP válido deve ser tentado antes do recovery code (não consulta recovery)")]
    public async Task Execute_WithValidTotp_ShouldNotQueryRecoveryCodes()
    {
        var user = MfaUser();
        SetupValidTotp("123456", 500);

        await NewSut().ExecuteAsync(user.Id, Code("123456"), Ip);

        _recoveryRepo.Verify(r => r.GetActiveByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Anti-replay ───────────────────────────────────────────────────────────

    [Theory(DisplayName = "TOTP reutilizado no mesmo step (ou anterior) deve ser rejeitado como falha")]
    [InlineData(500)]
    [InlineData(499)]
    public async Task Execute_WithReplayedStep_ShouldRejectAndCountFailure(long step)
    {
        var user = MfaUser();
        user.RegisterTotpStep(500);
        SetupValidTotp("123456", step);

        var act = () => NewSut().ExecuteAsync(user.Id, Code("123456"), Ip);

        await act.Should().ThrowAsync<DomainException>();
        user.LastUsedTotpStep.Should().Be(500);
        user.FailedLoginCount.Should().Be(1);
        _tokenSvc.Verify(t => t.Generate(It.IsAny<User>(), It.IsAny<IEnumerable<string>>()), Times.Never);
    }

    [Fact(DisplayName = "Conflito de concorrência ao gravar o step deve rejeitar sem reprocessar nem emitir token")]
    public async Task Execute_ConcurrencyConflict_ShouldRejectWithoutReprocessing()
    {
        var user = MfaUser();
        SetupValidTotp("123456", 500);
        _uow.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new ConcurrencyConflictException());

        var act = () => NewSut().ExecuteAsync(user.Id, Code("123456"), Ip);

        await act.Should().ThrowAsync<DomainException>();
        _userRepo.Verify(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()), Times.Once);
        _totp.Verify(t => t.ValidateCode(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>()), Times.Once);
        _tokenSvc.Verify(t => t.Generate(It.IsAny<User>(), It.IsAny<IEnumerable<string>>()), Times.Never);
    }

    // ── Falhas e lockout ──────────────────────────────────────────────────────

    [Fact(DisplayName = "Código inválido deve contar falha global, criar throttle do par e commitar")]
    public async Task Execute_WithInvalidCode_ShouldCountFailureGloballyAndPerPair()
    {
        var user = MfaUser();

        var act = () => NewSut().ExecuteAsync(user.Id, Code("000000"), Ip);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*inválid*");
        user.FailedLoginCount.Should().Be(1);
        _throttleRepo.Verify(r => r.TryAddAsync(
            It.Is<LoginThrottle>(t => t.UserId == user.Id && t.IpAddress == Ip && t.FailedCount == 1),
            It.IsAny<DateTime>(), It.IsAny<TimeSpan>(), It.IsAny<int>(), It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        _tokenSvc.Verify(t => t.Generate(It.IsAny<User>(), It.IsAny<IEnumerable<string>>()), Times.Never);
    }

    [Fact(DisplayName = "Código inválido com throttle existente deve incrementar o par e atualizar")]
    public async Task Execute_WithInvalidCode_ShouldUpdateExistingThrottle()
    {
        var user = MfaUser();
        var throttle = LoginThrottle.Create(user.Id, Ip, DateTime.UtcNow);
        throttle.RegisterFailure(5, Window, DateTime.UtcNow);
        _throttleRepo.Setup(r => r.GetAsync(user.Id, Ip, It.IsAny<CancellationToken>())).ReturnsAsync(throttle);

        var act = () => NewSut().ExecuteAsync(user.Id, Code("000000"), Ip);

        await act.Should().ThrowAsync<DomainException>();
        throttle.FailedCount.Should().Be(2);
        _throttleRepo.Verify(r => r.UpdateAsync(throttle, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "Falhas no verify devem bloquear a conta ao atingir o teto global configurado")]
    public async Task Execute_RepeatedFailures_ShouldLockAccountAtGlobalCeiling()
    {
        var user = MfaUser();
        var sut = NewSut(new LoginLockoutOptions { MaxFailedAttempts = 2, GlobalMaxFailedAttempts = 2, LockoutMinutes = 15 });

        for (var i = 0; i < 2; i++)
        {
            try { await sut.ExecuteAsync(user.Id, Code("000000"), Ip); }
            catch (DomainException) { /* esperado */ }
        }

        user.IsLockedOut(DateTime.UtcNow).Should().BeTrue();
    }

    [Fact(DisplayName = "Par (conta, IP) bloqueado deve rejeitar mesmo com código válido, sem validar o código")]
    public async Task Execute_WithLockedPair_ShouldRejectWithoutValidating()
    {
        var user = MfaUser();
        var throttle = LoginThrottle.Create(user.Id, Ip, DateTime.UtcNow);
        for (var i = 0; i < 5; i++) throttle.RegisterFailure(5, Window, DateTime.UtcNow);
        _throttleRepo.Setup(r => r.GetAsync(user.Id, Ip, It.IsAny<CancellationToken>())).ReturnsAsync(throttle);
        SetupValidTotp("123456", 500);

        var act = () => NewSut().ExecuteAsync(user.Id, Code("123456"), Ip);

        await act.Should().ThrowAsync<DomainException>();
        _totp.Verify(t => t.ValidateCode(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>()), Times.Never);
        _tokenSvc.Verify(t => t.Generate(It.IsAny<User>(), It.IsAny<IEnumerable<string>>()), Times.Never);
    }

    [Fact(DisplayName = "Conta bloqueada globalmente deve rejeitar mesmo com código válido, sem validar o código")]
    public async Task Execute_WithLockedAccount_ShouldRejectWithoutValidating()
    {
        var user = MfaUser();
        for (var i = 0; i < 50; i++) user.RegisterFailedLogin(50, Window, DateTime.UtcNow);
        SetupValidTotp("123456", 500);

        var act = () => NewSut().ExecuteAsync(user.Id, Code("123456"), Ip);

        await act.Should().ThrowAsync<DomainException>();
        _totp.Verify(t => t.ValidateCode(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>()), Times.Never);
        _tokenSvc.Verify(t => t.Generate(It.IsAny<User>(), It.IsAny<IEnumerable<string>>()), Times.Never);
    }

    // ── Recovery code ─────────────────────────────────────────────────────────

    [Fact(DisplayName = "Recovery code válido deve emitir o token completo e marcar o código como usado")]
    public async Task Execute_WithValidRecoveryCode_ShouldIssueTokenAndConsumeCode()
    {
        var user = MfaUser();
        var target = MfaRecoveryCode.Create(user.Id, "h:RECOVERY01");
        _recoveryRepo.Setup(r => r.GetActiveByUserIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MfaRecoveryCode> { MfaRecoveryCode.Create(user.Id, "h:OTHERCODE1"), target });

        var result = await NewSut().ExecuteAsync(user.Id, Code("RECOVERY01"), Ip);

        result.Token.Should().Be("full-jwt");
        target.IsUsed.Should().BeTrue();
        user.LastUsedTotpStep.Should().BeNull();
        _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "Recovery code é de uso único: o 2º uso do mesmo código deve ser rejeitado")]
    public async Task Execute_RecoveryCodeSecondUse_ShouldBeRejected()
    {
        var user = MfaUser();
        var codes = new List<MfaRecoveryCode> { MfaRecoveryCode.Create(user.Id, "h:RECOVERY01") };
        // O repositório devolve apenas códigos ainda não usados (como o real)
        _recoveryRepo.Setup(r => r.GetActiveByUserIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => codes.Where(c => !c.IsUsed).ToList());
        var sut = NewSut();

        await sut.ExecuteAsync(user.Id, Code("RECOVERY01"), Ip);
        var second = () => sut.ExecuteAsync(user.Id, Code("RECOVERY01"), Ip);

        await second.Should().ThrowAsync<DomainException>();
        _tokenSvc.Verify(t => t.Generate(It.IsAny<User>(), It.IsAny<IEnumerable<string>>()), Times.Once);
    }

    [Fact(DisplayName = "Recovery code inexistente deve contar como falha")]
    public async Task Execute_WithUnknownRecoveryCode_ShouldCountFailure()
    {
        var user = MfaUser();
        _recoveryRepo.Setup(r => r.GetActiveByUserIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MfaRecoveryCode> { MfaRecoveryCode.Create(user.Id, "h:RECOVERY01") });

        var act = () => NewSut().ExecuteAsync(user.Id, Code("WRONGCODE9"), Ip);

        await act.Should().ThrowAsync<DomainException>();
        user.FailedLoginCount.Should().Be(1);
        _tokenSvc.Verify(t => t.Generate(It.IsAny<User>(), It.IsAny<IEnumerable<string>>()), Times.Never);
    }

    // ── Pré-condições ─────────────────────────────────────────────────────────

    [Fact(DisplayName = "Usuário sem MFA ativo não deve obter token por esta rota")]
    public async Task Execute_WhenMfaNotEnabled_ShouldReject()
    {
        var user = User.Create("Caique", "caique@monkeybomb.com", "hashed_password");
        _userRepo.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var act = () => NewSut().ExecuteAsync(user.Id, Code("123456"), Ip);

        await act.Should().ThrowAsync<DomainException>();
        _tokenSvc.Verify(t => t.Generate(It.IsAny<User>(), It.IsAny<IEnumerable<string>>()), Times.Never);
    }

    [Fact(DisplayName = "Usuário inexistente deve ser rejeitado sem token")]
    public async Task Execute_WithUnknownUser_ShouldReject()
    {
        _userRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        var act = () => NewSut().ExecuteAsync(Guid.NewGuid(), Code("123456"), Ip);

        await act.Should().ThrowAsync<Exception>();
        _tokenSvc.Verify(t => t.Generate(It.IsAny<User>(), It.IsAny<IEnumerable<string>>()), Times.Never);
    }

    [Fact(DisplayName = "Usuário inativo/deletado deve ser rejeitado sem token")]
    public async Task Execute_WithInactiveUser_ShouldReject()
    {
        var user = MfaUser();
        user.SoftDelete();
        SetupValidTotp("123456", 500);

        var act = () => NewSut().ExecuteAsync(user.Id, Code("123456"), Ip);

        await act.Should().ThrowAsync<Exception>();
        _tokenSvc.Verify(t => t.Generate(It.IsAny<User>(), It.IsAny<IEnumerable<string>>()), Times.Never);
    }
}
