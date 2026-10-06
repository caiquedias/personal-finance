using FluentAssertions;
using Moq;
using PersonalFinance.Application.DTOs.Admin;
using PersonalFinance.Application.UseCases.Admin;
using PersonalFinance.Domain.Entities.Auth;
using PersonalFinance.Domain.Exceptions;
using PersonalFinance.Domain.Interfaces.Repositories;
using FluentValidation;
using PersonalFinance.Application.Tests.Unit.Support;
using PersonalFinance.Domain.Enums;
using System.Text.Json;
using Xunit;

namespace PersonalFinance.Application.Tests.Unit.Admin;

public class AssignRoleUseCaseTests
{
    private readonly Mock<IAdminUserRepository> _userRepo = new();
    private readonly Mock<IUserRoleRepository>  _roleRepo = new();
    private readonly Mock<IUnitOfWork>          _uow      = new();
    private readonly Mock<IAuditLogRepository> _audit = new();
    private const string Ip = "203.0.113.7";
    private static readonly Guid AdminId = Guid.NewGuid();
    private readonly AssignRoleUseCase          _sut;

    public AssignRoleUseCaseTests()
    {
        _sut = UseCaseFactory.Create<AssignRoleUseCase>(_userRepo.Object, _roleRepo.Object, _uow.Object, TestValidators.Valid<AssignRoleDto>(), _audit.Object);
    }

    [Fact(DisplayName = "Deve atribuir role a usuário ativo")]
    public async Task Execute_WithValidData_ShouldAssignRole()
    {
        var userId = Guid.NewGuid();
        var user   = User.Create("Caique", "caique@x.com", "hash");

        _userRepo.Setup(r => r.GetByIdAsync(userId, default)).ReturnsAsync(user);
        _roleRepo.Setup(r => r.UserHasRoleAsync(userId, 1, default)).ReturnsAsync(false);

        await _sut.ExecuteAsync(new AssignRoleDto(userId, 1), AdminId, Ip);

        _roleRepo.Verify(r => r.AssignAsync(
            It.Is<UserRole>(ur => ur.UserId == userId && ur.RoleId == 1),
            default), Times.Once);
        _uow.Verify(u => u.CommitAsync(default), Times.Once);
    }

    [Fact(DisplayName = "Deve lançar exceção se usuário já possui a role")]
    public async Task Execute_WithExistingRole_ShouldThrow()
    {
        var userId = Guid.NewGuid();
        var user   = User.Create("Caique", "caique@x.com", "hash");

        _userRepo.Setup(r => r.GetByIdAsync(userId, default)).ReturnsAsync(user);
        _roleRepo.Setup(r => r.UserHasRoleAsync(userId, 1, default)).ReturnsAsync(true);

        var act = () => _sut.ExecuteAsync(new AssignRoleDto(userId, 1), AdminId, Ip);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*já possui*");
        _uow.Verify(u => u.CommitAsync(default), Times.Never);
    }

    [Fact(DisplayName = "Deve lançar exceção se usuário estiver inativo")]
    public async Task Execute_WithInactiveUser_ShouldThrow()
    {
        var userId = Guid.NewGuid();
        var user   = User.Create("Caique", "caique@x.com", "hash");
        user.SoftDelete();

        _userRepo.Setup(r => r.GetByIdAsync(userId, default)).ReturnsAsync(user);

        var act = () => _sut.ExecuteAsync(new AssignRoleDto(userId, 1), AdminId, Ip);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*inativo*");
        _uow.Verify(u => u.CommitAsync(default), Times.Never);
    }

    [Fact(DisplayName = "Deve lançar exceção para usuário não encontrado")]
    public async Task Execute_WithNotFoundUser_ShouldThrow()
    {
        var userId = Guid.NewGuid();
        _userRepo.Setup(r => r.GetByIdAsync(userId, default)).ReturnsAsync((User?)null);

        var act = () => _sut.ExecuteAsync(new AssignRoleDto(userId, 1), AdminId, Ip);

        await act.Should().ThrowAsync<KeyNotFoundException>();
        _uow.Verify(u => u.CommitAsync(default), Times.Never);
    }

    // ── Validação (#396) ──────────────────────────────────────────────────────

    [Fact(DisplayName = "Deve lançar ValidationException e não atribuir role quando o validator reprova")]
    public async Task Execute_WhenValidatorFails_ShouldThrowValidationExceptionWithoutPersisting()
    {
        var sut = UseCaseFactory.Create<AssignRoleUseCase>(
            _userRepo.Object, _roleRepo.Object, _uow.Object,
            _audit.Object, TestValidators.Invalid<AssignRoleDto>());

        var act = () => sut.ExecuteAsync(new AssignRoleDto(Guid.NewGuid(), 0), AdminId, Ip);

        await act.Should().ThrowAsync<ValidationException>();
        _userRepo.Invocations.Should().BeEmpty();
        _roleRepo.Invocations.Should().BeEmpty();
        _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Auditoria (#402) ──────────────────────────────────────────────────────

    private void VerifyNoAudit() =>
        _audit.Verify(a => a.AddAsync(It.IsAny<AuditLog>(), It.IsAny<CancellationToken>()), Times.Never);

    [Fact(DisplayName = "Atribuir role deve gravar auditoria RoleAssigned com roleId antes do commit")]
    public async Task Execute_WithValidData_ShouldAuditBeforeCommit()
    {
        var userId = Guid.NewGuid();
        _userRepo.Setup(r => r.GetByIdAsync(userId, default)).ReturnsAsync(User.Create("Caique", "caique@x.com", "hash"));
        _roleRepo.Setup(r => r.UserHasRoleAsync(userId, 1, default)).ReturnsAsync(false);
        var trace = AuditTrace.Track(_audit, _uow);

        await _sut.ExecuteAsync(new AssignRoleDto(userId, 1), AdminId, Ip);

        var log = trace.Logs.Should().ContainSingle().Subject;
        log.Action.Should().Be(AuditAction.RoleAssigned);
        log.ActorUserId.Should().Be(AdminId);
        log.TargetUserId.Should().Be(userId);
        log.IpAddress.Should().Be(Ip);
        log.Details.Should().NotBeNull();
        using var doc = JsonDocument.Parse(log.Details!);
        doc.RootElement.GetProperty("roleId").GetInt32().Should().Be(1);
        log.Details.Should().NotContain("caique@x.com");
        trace.Order.Should().Equal("audit", "commit");
    }

    [Fact(DisplayName = "Não deve auditar quando a role já existe")]
    public async Task Execute_WithExistingRole_ShouldNotAudit()
    {
        var userId = Guid.NewGuid();
        _userRepo.Setup(r => r.GetByIdAsync(userId, default)).ReturnsAsync(User.Create("Caique", "caique@x.com", "hash"));
        _roleRepo.Setup(r => r.UserHasRoleAsync(userId, 1, default)).ReturnsAsync(true);

        var act = () => _sut.ExecuteAsync(new AssignRoleDto(userId, 1), AdminId, Ip);

        await act.Should().ThrowAsync<DomainException>();
        VerifyNoAudit();
    }

    [Fact(DisplayName = "Não deve auditar quando o usuário está inativo, não existe ou o validator reprova")]
    public async Task Execute_ErrorPaths_ShouldNotAudit()
    {
        var inactiveId = Guid.NewGuid();
        var inactive   = User.Create("Caique", "caique@x.com", "hash");
        inactive.SoftDelete();
        _userRepo.Setup(r => r.GetByIdAsync(inactiveId, default)).ReturnsAsync(inactive);
        var missingId = Guid.NewGuid();
        _userRepo.Setup(r => r.GetByIdAsync(missingId, default)).ReturnsAsync((User?)null);

        await ((Func<Task>)(() => _sut.ExecuteAsync(new AssignRoleDto(inactiveId, 1), AdminId, Ip)))
            .Should().ThrowAsync<DomainException>();
        await ((Func<Task>)(() => _sut.ExecuteAsync(new AssignRoleDto(missingId, 1), AdminId, Ip)))
            .Should().ThrowAsync<KeyNotFoundException>();

        var invalid = UseCaseFactory.Create<AssignRoleUseCase>(
            _userRepo.Object, _roleRepo.Object, _uow.Object, _audit.Object,
            TestValidators.Invalid<AssignRoleDto>());
        await ((Func<Task>)(() => invalid.ExecuteAsync(new AssignRoleDto(Guid.NewGuid(), 0), AdminId, Ip)))
            .Should().ThrowAsync<ValidationException>();

        VerifyNoAudit();
    }
}
