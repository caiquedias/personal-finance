using FluentAssertions;
using Moq;
using PersonalFinance.Application.DTOs.Auth;
using PersonalFinance.Application.UseCases.Auth;
using PersonalFinance.Domain.Entities.Auth;
using PersonalFinance.Domain.Exceptions;
using PersonalFinance.Domain.Interfaces.Repositories;
using PersonalFinance.Domain.Interfaces.Services;
using Xunit;

namespace PersonalFinance.Application.Tests.Unit.Auth;

/// <summary>
/// DisableMfaUseCase (#393): exige senha E (código TOTP ou recovery code).
/// Limpa secret, recovery codes e MfaEnabled.
/// </summary>
public class DisableMfaUseCaseTests
{
    private const string PlainSecret = "JBSWY3DPEHPK3PXPJBSWY3DPEHPK3PXP";

    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<IMfaRecoveryCodeRepository> _recoveryRepo = new();
    private readonly Mock<ITotpService> _totp = new();
    private readonly Mock<ISecretProtector> _protector = new();
    private readonly Mock<IPasswordHasher> _hasher = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly DisableMfaUseCase _sut;

    public DisableMfaUseCaseTests()
    {
        _protector.Setup(p => p.Unprotect("encrypted-blob")).Returns(PlainSecret);
        _hasher.Setup(h => h.Verify("Senha@123", "hashed_password")).Returns(true);

        _sut = new DisableMfaUseCase(
            _userRepo.Object, _recoveryRepo.Object, _totp.Object,
            _protector.Object, _hasher.Object, _uow.Object);
    }

    private User EnabledUser()
    {
        var user = User.Create("Caique", "caique@monkeybomb.com", "hashed_password");
        user.SetPendingMfaSecret("encrypted-blob");
        user.EnableMfa(DateTime.UtcNow);
        _userRepo.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _recoveryRepo.Setup(r => r.GetActiveByUserIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MfaRecoveryCode>());
        return user;
    }

    private void AssertStillEnabled(User user)
    {
        user.MfaEnabled.Should().BeTrue();
        user.MfaSecretEncrypted.Should().Be("encrypted-blob");
        _recoveryRepo.Verify(r => r.RemoveAllByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "Senha correta + TOTP válido deve desativar e limpar secret e recovery codes")]
    public async Task Execute_WithPasswordAndValidTotp_ShouldDisableAndClear()
    {
        var user = EnabledUser();
        _totp.Setup(t => t.ValidateCode(PlainSecret, "123456", It.IsAny<DateTime>())).Returns(700);

        await _sut.ExecuteAsync(user.Id, new DisableMfaDto("Senha@123", "123456"));

        user.MfaEnabled.Should().BeFalse();
        user.MfaSecretEncrypted.Should().BeNull();
        user.MfaEnabledAt.Should().BeNull();
        _recoveryRepo.Verify(r => r.RemoveAllByUserIdAsync(user.Id, It.IsAny<CancellationToken>()), Times.Once);
        _userRepo.Verify(r => r.UpdateAsync(user, It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "Senha correta + recovery code válido deve desativar")]
    public async Task Execute_WithPasswordAndRecoveryCode_ShouldDisable()
    {
        var user = EnabledUser();
        _totp.Setup(t => t.ValidateCode(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>())).Returns((long?)null);
        _recoveryRepo.Setup(r => r.GetActiveByUserIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MfaRecoveryCode>
            {
                MfaRecoveryCode.Create(user.Id, "h:OTHERCODE1"),
                MfaRecoveryCode.Create(user.Id, "h:RECOVERY01")
            });
        _hasher.Setup(h => h.Verify("RECOVERY01", "h:RECOVERY01")).Returns(true);

        await _sut.ExecuteAsync(user.Id, new DisableMfaDto("Senha@123", "RECOVERY01"));

        user.MfaEnabled.Should().BeFalse();
        user.MfaSecretEncrypted.Should().BeNull();
        _recoveryRepo.Verify(r => r.RemoveAllByUserIdAsync(user.Id, It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "Senha errada com código válido NÃO deve desativar")]
    public async Task Execute_WithWrongPassword_ShouldNotDisable()
    {
        var user = EnabledUser();
        _hasher.Setup(h => h.Verify("Errada", "hashed_password")).Returns(false);
        _totp.Setup(t => t.ValidateCode(PlainSecret, "123456", It.IsAny<DateTime>())).Returns(700);

        var act = () => _sut.ExecuteAsync(user.Id, new DisableMfaDto("Errada", "123456"));

        await act.Should().ThrowAsync<DomainException>();
        AssertStillEnabled(user);
    }

    [Fact(DisplayName = "Senha correta com código inválido (nem TOTP nem recovery) NÃO deve desativar")]
    public async Task Execute_WithInvalidCode_ShouldNotDisable()
    {
        var user = EnabledUser();
        _totp.Setup(t => t.ValidateCode(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>())).Returns((long?)null);

        var act = () => _sut.ExecuteAsync(user.Id, new DisableMfaDto("Senha@123", "000000"));

        await act.Should().ThrowAsync<DomainException>();
        AssertStillEnabled(user);
    }

    [Fact(DisplayName = "TOTP já usado (step <= último) deve ser rejeitado como replay")]
    public async Task Execute_WithReplayedTotpStep_ShouldNotDisable()
    {
        var user = EnabledUser();
        user.RegisterTotpStep(700);
        _totp.Setup(t => t.ValidateCode(PlainSecret, "123456", It.IsAny<DateTime>())).Returns(700);

        var act = () => _sut.ExecuteAsync(user.Id, new DisableMfaDto("Senha@123", "123456"));

        await act.Should().ThrowAsync<DomainException>();
        AssertStillEnabled(user);
    }

    [Fact(DisplayName = "Disable com MFA inativo deve lançar DomainException")]
    public async Task Execute_WhenMfaNotEnabled_ShouldThrow()
    {
        var user = User.Create("Caique", "caique@monkeybomb.com", "hashed_password");
        _userRepo.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var act = () => _sut.ExecuteAsync(user.Id, new DisableMfaDto("Senha@123", "123456"));

        await act.Should().ThrowAsync<DomainException>();
        _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "Usuário inexistente deve lançar KeyNotFoundException")]
    public async Task Execute_WithUnknownUser_ShouldThrowKeyNotFound()
    {
        _userRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        var act = () => _sut.ExecuteAsync(Guid.NewGuid(), new DisableMfaDto("Senha@123", "123456"));

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }
}
