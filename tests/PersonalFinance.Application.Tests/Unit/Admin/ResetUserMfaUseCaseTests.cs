using FluentAssertions;
using Moq;
using PersonalFinance.Application.UseCases.Admin;
using PersonalFinance.Domain.Entities.Auth;
using PersonalFinance.Domain.Exceptions;
using PersonalFinance.Domain.Interfaces.Repositories;
using PersonalFinance.Domain.Enums;
using System.Text.Json;
using PersonalFinance.Application.Tests.Unit.Support;
using FluentValidation;
using Xunit;

namespace PersonalFinance.Application.Tests.Unit.Admin;

public class ResetUserMfaUseCaseTests
{
    private readonly Mock<IAdminUserRepository>       _userRepo     = new();
    private readonly Mock<IMfaRecoveryCodeRepository> _recoveryRepo = new();
    private readonly Mock<IUnitOfWork>                _uow          = new();
    private readonly Mock<IAuditLogRepository> _audit = new();
    private const string Ip = "203.0.113.7";
    private readonly ResetUserMfaUseCase              _sut;

    private static readonly Guid AdminId = Guid.NewGuid();

    public ResetUserMfaUseCaseTests()
    {
        _sut = UseCaseFactory.Create<ResetUserMfaUseCase>(_userRepo.Object, _recoveryRepo.Object, _uow.Object, _audit.Object);
    }

    private static User NewUserWithMfa()
    {
        var user = User.Create("Target", "target@x.com", "hash");
        user.SetPendingMfaSecret("secret-cifrado");
        user.EnableMfa(DateTime.UtcNow);
        user.RegisterTotpStep(123);
        return user;
    }

    [Fact(DisplayName = "Deve resetar o MFA de outro usuário e limpar recovery codes")]
    public async Task Execute_WithMfaEnabled_ShouldClearMfaAndRecoveryCodes()
    {
        var user = NewUserWithMfa();
        _userRepo.Setup(r => r.GetByIdAsync(user.Id, default)).ReturnsAsync(user);

        await _sut.ExecuteAsync(user.Id, AdminId, Ip);

        user.MfaEnabled.Should().BeFalse();
        user.MfaSecretEncrypted.Should().BeNull();
        user.MfaEnabledAt.Should().BeNull();
        user.LastUsedTotpStep.Should().BeNull();
        _recoveryRepo.Verify(r => r.RemoveAllByUserIdAsync(user.Id, default), Times.Once);
        _uow.Verify(u => u.CommitAsync(default), Times.Once);
    }

    [Fact(DisplayName = "Deve resetar secret pendente quando MFA ainda não foi ativado")]
    public async Task Execute_WithPendingSecretOnly_ShouldReset()
    {
        var user = User.Create("Target", "target@x.com", "hash");
        user.SetPendingMfaSecret("secret-pendente");
        _userRepo.Setup(r => r.GetByIdAsync(user.Id, default)).ReturnsAsync(user);

        await _sut.ExecuteAsync(user.Id, AdminId, Ip);

        user.MfaSecretEncrypted.Should().BeNull();
        _recoveryRepo.Verify(r => r.RemoveAllByUserIdAsync(user.Id, default), Times.Once);
        _uow.Verify(u => u.CommitAsync(default), Times.Once);
    }

    [Fact(DisplayName = "Deve rotacionar o SecurityStamp do alvo com MFA ativo")]
    public async Task Execute_WithMfaEnabled_ShouldRotateSecurityStamp()
    {
        var user = NewUserWithMfa();
        var previous = user.SecurityStamp;
        _userRepo.Setup(r => r.GetByIdAsync(user.Id, default)).ReturnsAsync(user);

        await _sut.ExecuteAsync(user.Id, AdminId, Ip);

        user.SecurityStamp.Should().NotBe(previous);
        _uow.Verify(u => u.CommitAsync(default), Times.Once);
    }

    [Fact(DisplayName = "Deve rotacionar o SecurityStamp do alvo com secret pendente")]
    public async Task Execute_WithPendingSecretOnly_ShouldRotateSecurityStamp()
    {
        var user = User.Create("Target", "target@x.com", "hash");
        user.SetPendingMfaSecret("secret-pendente");
        var previous = user.SecurityStamp;
        _userRepo.Setup(r => r.GetByIdAsync(user.Id, default)).ReturnsAsync(user);

        await _sut.ExecuteAsync(user.Id, AdminId, Ip);

        user.SecurityStamp.Should().NotBe(previous);
        _uow.Verify(u => u.CommitAsync(default), Times.Once);
    }

    [Fact(DisplayName = "Não deve rotacionar o SecurityStamp quando o alvo não tem MFA nem secret")]
    public async Task Execute_WithoutMfaAndSecret_ShouldNotRotateSecurityStamp()
    {
        var user = User.Create("Target", "target@x.com", "hash");
        var previous = user.SecurityStamp;
        _userRepo.Setup(r => r.GetByIdAsync(user.Id, default)).ReturnsAsync(user);

        var act = () => _sut.ExecuteAsync(user.Id, AdminId, Ip);

        await act.Should().ThrowAsync<DomainException>();
        user.SecurityStamp.Should().Be(previous);
        _uow.Verify(u => u.CommitAsync(default), Times.Never);
    }

    [Fact(DisplayName = "Deve lançar exceção quando o alvo não tem MFA nem secret pendente")]
    public async Task Execute_WithoutMfaAndSecret_ShouldThrow()
    {
        var user = User.Create("Target", "target@x.com", "hash");
        _userRepo.Setup(r => r.GetByIdAsync(user.Id, default)).ReturnsAsync(user);

        var act = () => _sut.ExecuteAsync(user.Id, AdminId, Ip);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*MFA não está ativo*");
        _recoveryRepo.Verify(r => r.RemoveAllByUserIdAsync(It.IsAny<Guid>(), default), Times.Never);
        _uow.Verify(u => u.CommitAsync(default), Times.Never);
    }

    [Fact(DisplayName = "Não deve permitir admin resetar o próprio MFA por este endpoint")]
    public async Task Execute_AdminResettingOwnMfa_ShouldThrow()
    {
        var act = () => _sut.ExecuteAsync(AdminId, AdminId, Ip);

        await act.Should().ThrowAsync<DomainException>();
        _recoveryRepo.Verify(r => r.RemoveAllByUserIdAsync(It.IsAny<Guid>(), default), Times.Never);
        _uow.Verify(u => u.CommitAsync(default), Times.Never);
    }

    [Fact(DisplayName = "Deve lançar exceção para usuário não encontrado")]
    public async Task Execute_WithNotFoundUser_ShouldThrow()
    {
        var targetId = Guid.NewGuid();
        _userRepo.Setup(r => r.GetByIdAsync(targetId, default)).ReturnsAsync((User?)null);

        var act = () => _sut.ExecuteAsync(targetId, AdminId, Ip);

        await act.Should().ThrowAsync<KeyNotFoundException>();
        _uow.Verify(u => u.CommitAsync(default), Times.Never);
    }

    // ── Auditoria (#402) ──────────────────────────────────────────────────────

    private void VerifyNoAudit() =>
        _audit.Verify(a => a.AddAsync(It.IsAny<AuditLog>(), It.IsAny<CancellationToken>()), Times.Never);

    [Fact(DisplayName = "Reset de MFA deve gravar auditoria MfaReset sem secret/e-mail antes do commit")]
    public async Task Execute_WithMfaEnabled_ShouldAuditBeforeCommit()
    {
        var user = NewUserWithMfa();
        _userRepo.Setup(r => r.GetByIdAsync(user.Id, default)).ReturnsAsync(user);
        var trace = AuditTrace.Track(_audit, _uow);

        await _sut.ExecuteAsync(user.Id, AdminId, Ip);

        var log = trace.Logs.Should().ContainSingle().Subject;
        log.Action.Should().Be(AuditAction.MfaReset);
        log.ActorUserId.Should().Be(AdminId);
        log.TargetUserId.Should().Be(user.Id);
        log.IpAddress.Should().Be(Ip);
        (log.Details ?? string.Empty).Should().NotContain("secret-cifrado").And.NotContain("target@x.com");
        trace.Order.Should().Equal("audit", "commit");
    }

    [Fact(DisplayName = "Não deve auditar nos caminhos de erro (auto-reset, MFA inativo, não encontrado)")]
    public async Task Execute_ErrorPaths_ShouldNotAudit()
    {
        var inactive = User.Create("Target", "target@x.com", "hash");
        _userRepo.Setup(r => r.GetByIdAsync(inactive.Id, default)).ReturnsAsync(inactive);
        var missingId = Guid.NewGuid();
        _userRepo.Setup(r => r.GetByIdAsync(missingId, default)).ReturnsAsync((User?)null);

        await ((Func<Task>)(() => _sut.ExecuteAsync(AdminId, AdminId, Ip))).Should().ThrowAsync<DomainException>();
        await ((Func<Task>)(() => _sut.ExecuteAsync(inactive.Id, AdminId, Ip))).Should().ThrowAsync<DomainException>();
        await ((Func<Task>)(() => _sut.ExecuteAsync(missingId, AdminId, Ip))).Should().ThrowAsync<KeyNotFoundException>();

        VerifyNoAudit();
    }
}
