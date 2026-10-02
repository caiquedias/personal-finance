using FluentAssertions;
using Moq;
using PersonalFinance.Application.UseCases.Admin;
using PersonalFinance.Domain.Entities.Auth;
using PersonalFinance.Domain.Exceptions;
using PersonalFinance.Domain.Interfaces.Repositories;
using Xunit;

namespace PersonalFinance.Application.Tests.Unit.Admin;

public class ResetUserMfaUseCaseTests
{
    private readonly Mock<IAdminUserRepository>       _userRepo     = new();
    private readonly Mock<IMfaRecoveryCodeRepository> _recoveryRepo = new();
    private readonly Mock<IUnitOfWork>                _uow          = new();
    private readonly ResetUserMfaUseCase              _sut;

    private static readonly Guid AdminId = Guid.NewGuid();

    public ResetUserMfaUseCaseTests()
    {
        _sut = new ResetUserMfaUseCase(_userRepo.Object, _recoveryRepo.Object, _uow.Object);
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

        await _sut.ExecuteAsync(user.Id, AdminId);

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

        await _sut.ExecuteAsync(user.Id, AdminId);

        user.MfaSecretEncrypted.Should().BeNull();
        _recoveryRepo.Verify(r => r.RemoveAllByUserIdAsync(user.Id, default), Times.Once);
        _uow.Verify(u => u.CommitAsync(default), Times.Once);
    }

    [Fact(DisplayName = "Deve lançar exceção quando o alvo não tem MFA nem secret pendente")]
    public async Task Execute_WithoutMfaAndSecret_ShouldThrow()
    {
        var user = User.Create("Target", "target@x.com", "hash");
        _userRepo.Setup(r => r.GetByIdAsync(user.Id, default)).ReturnsAsync(user);

        var act = () => _sut.ExecuteAsync(user.Id, AdminId);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*MFA não está ativo*");
        _recoveryRepo.Verify(r => r.RemoveAllByUserIdAsync(It.IsAny<Guid>(), default), Times.Never);
        _uow.Verify(u => u.CommitAsync(default), Times.Never);
    }

    [Fact(DisplayName = "Não deve permitir admin resetar o próprio MFA por este endpoint")]
    public async Task Execute_AdminResettingOwnMfa_ShouldThrow()
    {
        var act = () => _sut.ExecuteAsync(AdminId, AdminId);

        await act.Should().ThrowAsync<DomainException>();
        _recoveryRepo.Verify(r => r.RemoveAllByUserIdAsync(It.IsAny<Guid>(), default), Times.Never);
        _uow.Verify(u => u.CommitAsync(default), Times.Never);
    }

    [Fact(DisplayName = "Deve lançar exceção para usuário não encontrado")]
    public async Task Execute_WithNotFoundUser_ShouldThrow()
    {
        var targetId = Guid.NewGuid();
        _userRepo.Setup(r => r.GetByIdAsync(targetId, default)).ReturnsAsync((User?)null);

        var act = () => _sut.ExecuteAsync(targetId, AdminId);

        await act.Should().ThrowAsync<KeyNotFoundException>();
        _uow.Verify(u => u.CommitAsync(default), Times.Never);
    }
}
