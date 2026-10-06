using FluentAssertions;
using Moq;
using PersonalFinance.Application.DTOs.Admin;
using PersonalFinance.Application.UseCases.Admin;
using PersonalFinance.Domain.Exceptions;
using PersonalFinance.Domain.Interfaces.Repositories;
using PersonalFinance.Domain.Enums;
using System.Text.Json;
using PersonalFinance.Application.Tests.Unit.Support;
using PersonalFinance.Domain.Entities.Auth;
using FluentValidation;
using Xunit;

namespace PersonalFinance.Application.Tests.Unit.Admin;

public class RemoveRoleUseCaseTests
{
    private readonly Mock<IUserRoleRepository> _roleRepo = new();
    private readonly Mock<IUnitOfWork>         _uow      = new();
    private readonly Mock<IAuditLogRepository> _audit = new();
    private const string Ip = "203.0.113.7";
    private readonly RemoveRoleUseCase         _sut;

    private static readonly Guid AdminId = Guid.NewGuid();

    public RemoveRoleUseCaseTests()
    {
        _sut = UseCaseFactory.Create<RemoveRoleUseCase>(_roleRepo.Object, _uow.Object, _audit.Object);
    }

    [Fact(DisplayName = "Deve remover role de outro usuário")]
    public async Task Execute_WithValidData_ShouldRemoveRole()
    {
        var targetId = Guid.NewGuid();
        _roleRepo.Setup(r => r.UserHasRoleAsync(targetId, 2, default)).ReturnsAsync(true);

        await _sut.ExecuteAsync(new RemoveRoleDto(targetId, 2), AdminId, Ip);

        _roleRepo.Verify(r => r.RemoveAsync(targetId, 2, default), Times.Once);
        _uow.Verify(u => u.CommitAsync(default), Times.Once);
    }

    [Fact(DisplayName = "Não deve permitir admin remover a própria role Admin")]
    public async Task Execute_AdminRemovingOwnAdminRole_ShouldThrow()
    {
        var act = () => _sut.ExecuteAsync(new RemoveRoleDto(AdminId, 1), AdminId, Ip);

        await act.Should().ThrowAsync<DomainException>()
                 .WithMessage("*própria role Admin*");
        _uow.Verify(u => u.CommitAsync(default), Times.Never);
    }

    [Fact(DisplayName = "Deve lançar exceção se usuário não possui a role")]
    public async Task Execute_WithRoleNotOwned_ShouldThrow()
    {
        var targetId = Guid.NewGuid();
        _roleRepo.Setup(r => r.UserHasRoleAsync(targetId, 2, default)).ReturnsAsync(false);

        var act = () => _sut.ExecuteAsync(new RemoveRoleDto(targetId, 2), AdminId, Ip);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*não possui*");
        _uow.Verify(u => u.CommitAsync(default), Times.Never);
    }

    [Fact(DisplayName = "Admin pode remover role Admin de outro usuário")]
    public async Task Execute_AdminRemovingOtherUsersAdminRole_ShouldSucceed()
    {
        var otherId = Guid.NewGuid();
        _roleRepo.Setup(r => r.UserHasRoleAsync(otherId, 1, default)).ReturnsAsync(true);

        await _sut.ExecuteAsync(new RemoveRoleDto(otherId, 1), AdminId, Ip);

        _roleRepo.Verify(r => r.RemoveAsync(otherId, 1, default), Times.Once);
        _uow.Verify(u => u.CommitAsync(default), Times.Once);
    }

    // ── Auditoria (#402) ──────────────────────────────────────────────────────

    private void VerifyNoAudit() =>
        _audit.Verify(a => a.AddAsync(It.IsAny<AuditLog>(), It.IsAny<CancellationToken>()), Times.Never);

    [Fact(DisplayName = "Remover role deve gravar auditoria RoleRemoved com roleId antes do commit")]
    public async Task Execute_WithValidData_ShouldAuditBeforeCommit()
    {
        var targetId = Guid.NewGuid();
        _roleRepo.Setup(r => r.UserHasRoleAsync(targetId, 2, default)).ReturnsAsync(true);
        var trace = AuditTrace.Track(_audit, _uow);

        await _sut.ExecuteAsync(new RemoveRoleDto(targetId, 2), AdminId, Ip);

        var log = trace.Logs.Should().ContainSingle().Subject;
        log.Action.Should().Be(AuditAction.RoleRemoved);
        log.ActorUserId.Should().Be(AdminId);
        log.TargetUserId.Should().Be(targetId);
        log.IpAddress.Should().Be(Ip);
        log.Details.Should().NotBeNull();
        using var doc = JsonDocument.Parse(log.Details!);
        doc.RootElement.GetProperty("roleId").GetInt32().Should().Be(2);
        trace.Order.Should().Equal("audit", "commit");
    }

    [Fact(DisplayName = "Não deve auditar ao tentar remover a própria role Admin")]
    public async Task Execute_OwnAdminRole_ShouldNotAudit()
    {
        var act = () => _sut.ExecuteAsync(new RemoveRoleDto(AdminId, 1), AdminId, Ip);

        await act.Should().ThrowAsync<DomainException>();
        VerifyNoAudit();
    }

    [Fact(DisplayName = "Não deve auditar quando o usuário não possui a role")]
    public async Task Execute_UserWithoutRole_ShouldNotAudit()
    {
        var targetId = Guid.NewGuid();
        _roleRepo.Setup(r => r.UserHasRoleAsync(targetId, 2, default)).ReturnsAsync(false);

        var act = () => _sut.ExecuteAsync(new RemoveRoleDto(targetId, 2), AdminId, Ip);

        await act.Should().ThrowAsync<DomainException>();
        VerifyNoAudit();
    }
}
