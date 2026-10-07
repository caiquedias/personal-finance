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

public class ToggleUserActiveUseCaseTests
{
    private readonly Mock<IAdminUserRepository> _userRepo = new();
    private readonly Mock<IUnitOfWork>          _uow      = new();
    private readonly Mock<IAuditLogRepository> _audit = new();
    private const string Ip = "203.0.113.7";
    private readonly ToggleUserActiveUseCase    _sut;

    private static readonly Guid AdminId = Guid.NewGuid();

    public ToggleUserActiveUseCaseTests()
    {
        _sut = UseCaseFactory.Create<ToggleUserActiveUseCase>(_userRepo.Object, _uow.Object, _audit.Object);
    }

    [Fact(DisplayName = "Deve desativar usuário ativo")]
    public async Task Execute_WithActiveUser_ShouldDeactivate()
    {
        var targetId = Guid.NewGuid();
        var user     = User.Create("Target", "target@x.com", "hash");

        _userRepo.Setup(r => r.GetByIdAsync(targetId, default)).ReturnsAsync(user);

        await _sut.ExecuteAsync(targetId, AdminId, Ip);

        user.IsActive.Should().BeFalse();
        user.IsDeleted.Should().BeTrue();
        _uow.Verify(u => u.CommitAsync(default), Times.Once);
    }

    [Fact(DisplayName = "Deve rotacionar o SecurityStamp ao desativar usuário")]
    public async Task Execute_WithActiveUser_ShouldRotateSecurityStamp()
    {
        var targetId = Guid.NewGuid();
        var user     = User.Create("Target", "target@x.com", "hash");
        var previous = user.SecurityStamp;

        _userRepo.Setup(r => r.GetByIdAsync(targetId, default)).ReturnsAsync(user);

        await _sut.ExecuteAsync(targetId, AdminId, Ip);

        user.SecurityStamp.Should().NotBe(previous);
        _uow.Verify(u => u.CommitAsync(default), Times.Once);
    }

    [Fact(DisplayName = "Deve reativar usuário inativo")]
    public async Task Execute_WithInactiveUser_ShouldReactivate()
    {
        var targetId = Guid.NewGuid();
        var user     = User.Create("Target", "target@x.com", "hash");
        user.SoftDelete();

        _userRepo.Setup(r => r.GetByIdAsync(targetId, default)).ReturnsAsync(user);

        await _sut.ExecuteAsync(targetId, AdminId, Ip);

        user.IsActive.Should().BeTrue();
        user.IsDeleted.Should().BeFalse();
        _uow.Verify(u => u.CommitAsync(default), Times.Once);
    }

    [Fact(DisplayName = "Não deve permitir admin desativar a si próprio")]
    public async Task Execute_AdminTargetingSelf_ShouldThrow()
    {
        var act = () => _sut.ExecuteAsync(AdminId, AdminId, Ip);

        await act.Should().ThrowAsync<DomainException>()
                 .WithMessage("*administrador*");
        _uow.Verify(u => u.CommitAsync(default), Times.Never);
    }

    [Fact(DisplayName = "Deve lançar exceção para usuário não encontrado")]
    public async Task Execute_WithNotFoundUser_ShouldThrow()
    {
        var targetId = Guid.NewGuid();
        _userRepo.Setup(r => r.GetByIdAsync(targetId, default))
                 .ReturnsAsync((User?)null);

        var act = () => _sut.ExecuteAsync(targetId, AdminId, Ip);

        await act.Should().ThrowAsync<KeyNotFoundException>();
        _uow.Verify(u => u.CommitAsync(default), Times.Never);
    }

    // ── Auditoria (#402) ──────────────────────────────────────────────────────

    private void VerifyNoAudit() =>
        _audit.Verify(a => a.AddAsync(It.IsAny<AuditLog>(), It.IsAny<CancellationToken>()), Times.Never);

    [Fact(DisplayName = "Desativar usuário deve gravar auditoria UserDeactivated antes do commit")]
    public async Task Execute_Deactivate_ShouldAuditBeforeCommit()
    {
        var targetId = Guid.NewGuid();
        var user     = User.Create("Target", "target@x.com", "hash");
        _userRepo.Setup(r => r.GetByIdAsync(targetId, default)).ReturnsAsync(user);
        var trace = AuditTrace.Track(_audit, _uow);

        await _sut.ExecuteAsync(targetId, AdminId, Ip);

        var log = trace.Logs.Should().ContainSingle().Subject;
        log.Action.Should().Be(AuditAction.UserDeactivated);
        log.ActorUserId.Should().Be(AdminId);
        log.TargetUserId.Should().Be(targetId);
        log.IpAddress.Should().Be(Ip);
        trace.Order.Should().Equal("audit", "commit");
    }

    [Fact(DisplayName = "Reativar usuário deve gravar auditoria UserActivated antes do commit")]
    public async Task Execute_Reactivate_ShouldAuditBeforeCommit()
    {
        var targetId = Guid.NewGuid();
        var user     = User.Create("Target", "target@x.com", "hash");
        user.SoftDelete();
        _userRepo.Setup(r => r.GetByIdAsync(targetId, default)).ReturnsAsync(user);
        var trace = AuditTrace.Track(_audit, _uow);

        await _sut.ExecuteAsync(targetId, AdminId, Ip);

        var log = trace.Logs.Should().ContainSingle().Subject;
        log.Action.Should().Be(AuditAction.UserActivated);
        log.ActorUserId.Should().Be(AdminId);
        log.TargetUserId.Should().Be(targetId);
        trace.Order.Should().Equal("audit", "commit");
    }

    [Fact(DisplayName = "IP nulo deve ser gravado como nulo na auditoria")]
    public async Task Execute_WithNullIp_ShouldAuditWithNullIp()
    {
        var targetId = Guid.NewGuid();
        _userRepo.Setup(r => r.GetByIdAsync(targetId, default))
                 .ReturnsAsync(User.Create("Target", "target@x.com", "hash"));
        var trace = AuditTrace.Track(_audit, _uow);

        await _sut.ExecuteAsync(targetId, AdminId, null);

        trace.Logs.Should().ContainSingle().Which.IpAddress.Should().BeNull();
    }

    [Fact(DisplayName = "Não deve auditar na auto-desativação")]
    public async Task Execute_SelfTarget_ShouldNotAudit()
    {
        var act = () => _sut.ExecuteAsync(AdminId, AdminId, Ip);

        await act.Should().ThrowAsync<DomainException>();
        VerifyNoAudit();
    }

    [Fact(DisplayName = "Não deve auditar quando o alvo não existe")]
    public async Task Execute_NotFound_ShouldNotAudit()
    {
        var targetId = Guid.NewGuid();
        _userRepo.Setup(r => r.GetByIdAsync(targetId, default)).ReturnsAsync((User?)null);

        var act = () => _sut.ExecuteAsync(targetId, AdminId, Ip);

        await act.Should().ThrowAsync<KeyNotFoundException>();
        VerifyNoAudit();
    }
}
