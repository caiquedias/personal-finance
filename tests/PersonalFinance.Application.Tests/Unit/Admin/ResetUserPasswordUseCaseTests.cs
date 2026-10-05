using FluentAssertions;
using Moq;
using PersonalFinance.Application.DTOs.Admin;
using PersonalFinance.Application.UseCases.Admin;
using PersonalFinance.Domain.Entities.Auth;
using PersonalFinance.Domain.Exceptions;
using PersonalFinance.Domain.Interfaces.Repositories;
using PersonalFinance.Domain.Interfaces.Services;
using FluentValidation;
using PersonalFinance.Application.Tests.Unit.Support;
using PersonalFinance.Domain.Enums;
using System.Text.Json;
using Xunit;

namespace PersonalFinance.Application.Tests.Unit.Admin;

public class ResetUserPasswordUseCaseTests
{
    private readonly Mock<IAdminUserRepository> _userRepo = new();
    private readonly Mock<IPasswordHasher>      _hasher   = new();
    private readonly Mock<IUnitOfWork>          _uow      = new();
    private readonly Mock<IAuditLogRepository> _audit = new();
    private const string Ip = "203.0.113.7";
    private readonly ResetUserPasswordUseCase   _sut;

    private static readonly Guid AdminId = Guid.NewGuid();

    public ResetUserPasswordUseCaseTests()
    {
        _sut = UseCaseFactory.Create<ResetUserPasswordUseCase>(_userRepo.Object, _hasher.Object, _uow.Object, TestValidators.Valid<ResetPasswordDto>(), _audit.Object);
    }

    [Fact(DisplayName = "Deve resetar senha de outro usuário")]
    public async Task Execute_WithValidData_ShouldUpdatePasswordHash()
    {
        var targetId = Guid.NewGuid();
        var user     = User.Create("Target", "target@x.com", "old_hash");

        _userRepo.Setup(r => r.GetByIdAsync(targetId, default)).ReturnsAsync(user);
        _hasher.Setup(h => h.Hash("NovaSenha@123")).Returns("new_hash");

        await _sut.ExecuteAsync(new ResetPasswordDto(targetId, "NovaSenha@123"), AdminId, Ip);

        user.PasswordHash.Should().Be("new_hash");
        _uow.Verify(u => u.CommitAsync(default), Times.Once);
    }

    [Fact(DisplayName = "Deve rotacionar o SecurityStamp do alvo ao resetar a senha")]
    public async Task Execute_WithValidData_ShouldRotateSecurityStamp()
    {
        var targetId = Guid.NewGuid();
        var user     = User.Create("Target", "target@x.com", "old_hash");
        var previous = user.SecurityStamp;

        _userRepo.Setup(r => r.GetByIdAsync(targetId, default)).ReturnsAsync(user);
        _hasher.Setup(h => h.Hash("NovaSenha@123")).Returns("new_hash");

        await _sut.ExecuteAsync(new ResetPasswordDto(targetId, "NovaSenha@123"), AdminId, Ip);

        user.SecurityStamp.Should().NotBe(previous);
        _uow.Verify(u => u.CommitAsync(default), Times.Once);
    }

    [Fact(DisplayName = "Deve chamar o hasher antes de atualizar")]
    public async Task Execute_ShouldHashPasswordBeforeUpdate()
    {
        var targetId = Guid.NewGuid();
        var user     = User.Create("Target", "target@x.com", "old_hash");

        _userRepo.Setup(r => r.GetByIdAsync(targetId, default)).ReturnsAsync(user);
        _hasher.Setup(h => h.Hash("NovaSenha@123")).Returns("hashed");

        await _sut.ExecuteAsync(new ResetPasswordDto(targetId, "NovaSenha@123"), AdminId, Ip);

        _hasher.Verify(h => h.Hash("NovaSenha@123"), Times.Once);
    }

    [Fact(DisplayName = "Não deve permitir admin resetar a própria senha por este endpoint")]
    public async Task Execute_AdminResettingOwnPassword_ShouldThrow()
    {
        var act = () => _sut.ExecuteAsync(
            new ResetPasswordDto(AdminId, "NovaSenha@123"), AdminId, Ip);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*perfil*");
        _uow.Verify(u => u.CommitAsync(default), Times.Never);
    }

    [Fact(DisplayName = "Deve lançar exceção para senha menor que 8 caracteres")]
    public async Task Execute_WithShortPassword_ShouldThrow()
    {
        var targetId = Guid.NewGuid();

        var act = () => _sut.ExecuteAsync(
            new ResetPasswordDto(targetId, "abc"), AdminId, Ip);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*8*");
        _uow.Verify(u => u.CommitAsync(default), Times.Never);
    }

    [Fact(DisplayName = "Deve lançar exceção para usuário não encontrado")]
    public async Task Execute_WithNotFoundUser_ShouldThrow()
    {
        var targetId = Guid.NewGuid();
        _userRepo.Setup(r => r.GetByIdAsync(targetId, default)).ReturnsAsync((User?)null);

        var act = () => _sut.ExecuteAsync(
            new ResetPasswordDto(targetId, "NovaSenha@123"), AdminId, Ip);

        await act.Should().ThrowAsync<KeyNotFoundException>();
        _uow.Verify(u => u.CommitAsync(default), Times.Never);
    }

    // ── Validação (#396) ──────────────────────────────────────────────────────

    [Fact(DisplayName = "Deve lançar ValidationException e não alterar senha quando o validator reprova")]
    public async Task Execute_WhenValidatorFails_ShouldThrowValidationExceptionWithoutPersisting()
    {
        var sut = UseCaseFactory.Create<ResetUserPasswordUseCase>(
            _userRepo.Object, _hasher.Object, _uow.Object,
            _audit.Object, TestValidators.Invalid<ResetPasswordDto>());

        var act = () => sut.ExecuteAsync(new ResetPasswordDto(Guid.NewGuid(), "NovaSenha@123"), AdminId, Ip);

        await act.Should().ThrowAsync<ValidationException>();
        _userRepo.Invocations.Should().BeEmpty();
        _hasher.Invocations.Should().BeEmpty();
        _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Auditoria (#402) ──────────────────────────────────────────────────────

    private void VerifyNoAudit() =>
        _audit.Verify(a => a.AddAsync(It.IsAny<AuditLog>(), It.IsAny<CancellationToken>()), Times.Never);

    [Fact(DisplayName = "Reset de senha deve gravar auditoria PasswordReset sem senha/hash/e-mail antes do commit")]
    public async Task Execute_WithValidData_ShouldAuditBeforeCommit()
    {
        var targetId = Guid.NewGuid();
        var user     = User.Create("Target", "target@x.com", "hash-antigo");
        _userRepo.Setup(r => r.GetByIdAsync(targetId, default)).ReturnsAsync(user);
        _hasher.Setup(h => h.Hash("NovaSenha@123")).Returns("novo-hash-secreto");
        var trace = AuditTrace.Track(_audit, _uow);

        await _sut.ExecuteAsync(new ResetPasswordDto(targetId, "NovaSenha@123"), AdminId, Ip);

        var log = trace.Logs.Should().ContainSingle().Subject;
        log.Action.Should().Be(AuditAction.PasswordReset);
        log.ActorUserId.Should().Be(AdminId);
        log.TargetUserId.Should().Be(targetId);
        log.IpAddress.Should().Be(Ip);
        (log.Details ?? string.Empty).Should().NotContain("NovaSenha@123")
            .And.NotContain("novo-hash-secreto").And.NotContain("hash-antigo").And.NotContain("target@x.com");
        trace.Order.Should().Equal("audit", "commit");
    }

    [Fact(DisplayName = "Não deve auditar nos caminhos de erro (auto-reset, senha curta, não encontrado, validator)")]
    public async Task Execute_ErrorPaths_ShouldNotAudit()
    {
        var missingId = Guid.NewGuid();
        _userRepo.Setup(r => r.GetByIdAsync(missingId, default)).ReturnsAsync((User?)null);

        await ((Func<Task>)(() => _sut.ExecuteAsync(new ResetPasswordDto(AdminId, "NovaSenha@123"), AdminId, Ip)))
            .Should().ThrowAsync<DomainException>();
        await ((Func<Task>)(() => _sut.ExecuteAsync(new ResetPasswordDto(Guid.NewGuid(), "abc"), AdminId, Ip)))
            .Should().ThrowAsync<DomainException>();
        await ((Func<Task>)(() => _sut.ExecuteAsync(new ResetPasswordDto(missingId, "NovaSenha@123"), AdminId, Ip)))
            .Should().ThrowAsync<KeyNotFoundException>();

        var invalid = UseCaseFactory.Create<ResetUserPasswordUseCase>(
            _userRepo.Object, _hasher.Object, _uow.Object, _audit.Object,
            TestValidators.Invalid<ResetPasswordDto>());
        await ((Func<Task>)(() => invalid.ExecuteAsync(new ResetPasswordDto(Guid.NewGuid(), "NovaSenha@123"), AdminId, Ip)))
            .Should().ThrowAsync<ValidationException>();

        VerifyNoAudit();
    }
}
