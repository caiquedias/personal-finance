using FluentAssertions;
using Moq;
using PersonalFinance.Application.UseCases.Auth;
using PersonalFinance.Domain.Entities.Auth;
using PersonalFinance.Domain.Exceptions;
using PersonalFinance.Domain.Interfaces.Repositories;
using PersonalFinance.Domain.Interfaces.Services;
using Xunit;

namespace PersonalFinance.Application.Tests.Unit.Auth;

/// <summary>
/// SetupMfaUseCase (#393): gera o secret TOTP, guarda CIFRADO como pendente (MfaEnabled=false)
/// e devolve o secret em claro + URI otpauth uma única vez para o app autenticador.
/// </summary>
public class SetupMfaUseCaseTests
{
    private const string Email = "caique@monkeybomb.com";
    private const string PlainSecret = "JBSWY3DPEHPK3PXPJBSWY3DPEHPK3PXP";
    private const string OtpAuthUri = "otpauth://totp/MonkeyBomb:caique@monkeybomb.com?secret=JBSWY3DPEHPK3PXPJBSWY3DPEHPK3PXP&issuer=MonkeyBomb";

    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<ITotpService> _totp = new();
    private readonly Mock<ISecretProtector> _protector = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly SetupMfaUseCase _sut;

    public SetupMfaUseCaseTests()
    {
        _totp.Setup(t => t.GenerateSecret()).Returns(PlainSecret);
        _totp.Setup(t => t.BuildOtpAuthUri(PlainSecret, Email)).Returns(OtpAuthUri);
        _protector.Setup(p => p.Protect(PlainSecret)).Returns("encrypted-blob");

        _sut = new SetupMfaUseCase(_userRepo.Object, _totp.Object, _protector.Object, _uow.Object);
    }

    private static User FakeUser() => User.Create("Caique", Email, "hashed_password");

    [Fact(DisplayName = "Deve devolver secret e URI otpauth e guardar o secret cifrado como pendente")]
    public async Task Execute_ShouldReturnSecretAndStoreEncryptedPending()
    {
        var user = FakeUser();
        _userRepo.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var result = await _sut.ExecuteAsync(user.Id);

        result.Secret.Should().Be(PlainSecret);
        result.OtpAuthUri.Should().Be(OtpAuthUri);
        user.MfaSecretEncrypted.Should().Be("encrypted-blob");
        user.MfaSecretEncrypted.Should().NotBe(PlainSecret);
        user.MfaEnabled.Should().BeFalse();
        _userRepo.Verify(r => r.UpdateAsync(user, It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "Setup repetido com secret pendente deve substituí-lo por um novo")]
    public async Task Execute_WithPendingSecret_ShouldReplaceIt()
    {
        var user = FakeUser();
        user.SetPendingMfaSecret("old-blob");
        _userRepo.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        await _sut.ExecuteAsync(user.Id);

        user.MfaSecretEncrypted.Should().Be("encrypted-blob");
        user.MfaEnabled.Should().BeFalse();
    }

    [Fact(DisplayName = "Setup com MFA já ativo deve lançar DomainException sem sobrescrever o secret")]
    public async Task Execute_WhenMfaAlreadyEnabled_ShouldThrowAndNotOverwrite()
    {
        var user = FakeUser();
        user.SetPendingMfaSecret("active-blob");
        user.EnableMfa(DateTime.UtcNow);
        _userRepo.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var act = () => _sut.ExecuteAsync(user.Id);

        await act.Should().ThrowAsync<DomainException>();
        user.MfaSecretEncrypted.Should().Be("active-blob");
        _totp.Verify(t => t.GenerateSecret(), Times.Never);
        _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "Usuário inexistente deve lançar KeyNotFoundException")]
    public async Task Execute_WithUnknownUser_ShouldThrowKeyNotFound()
    {
        _userRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        var act = () => _sut.ExecuteAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<KeyNotFoundException>();
        _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "O secret em claro nunca deve ser atribuído ao usuário")]
    public async Task Execute_ShouldNeverPersistPlainSecret()
    {
        var user = FakeUser();
        _userRepo.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        await _sut.ExecuteAsync(user.Id);

        _protector.Verify(p => p.Protect(PlainSecret), Times.Once);
        user.MfaSecretEncrypted.Should().NotContain(PlainSecret);
    }
}
