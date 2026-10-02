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
/// EnableMfaUseCase (#393): valida o 1º código TOTP contra o secret pendente, ativa o MFA e gera os
/// recovery codes (exibidos UMA vez; só o hash é persistido). Regerar invalida os anteriores.
/// </summary>
public class EnableMfaUseCaseTests
{
    private const string PlainSecret = "JBSWY3DPEHPK3PXPJBSWY3DPEHPK3PXP";

    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<IMfaRecoveryCodeRepository> _recoveryRepo = new();
    private readonly Mock<ITotpService> _totp = new();
    private readonly Mock<ISecretProtector> _protector = new();
    private readonly Mock<IPasswordHasher> _hasher = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly EnableMfaUseCase _sut;

    public EnableMfaUseCaseTests()
    {
        _protector.Setup(p => p.Unprotect("encrypted-blob")).Returns(PlainSecret);
        _hasher.Setup(h => h.Hash(It.IsAny<string>())).Returns((string s) => "h:" + s);

        _sut = new EnableMfaUseCase(
            _userRepo.Object, _recoveryRepo.Object, _totp.Object,
            _protector.Object, _hasher.Object, _uow.Object);
    }

    private static User UserWithPendingSecret()
    {
        var user = User.Create("Caique", "caique@monkeybomb.com", "hashed_password");
        user.SetPendingMfaSecret("encrypted-blob");
        return user;
    }

    private User SetupUser(User user)
    {
        _userRepo.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        return user;
    }

    private void SetupValidCode(string code = "123456", long step = 500) =>
        _totp.Setup(t => t.ValidateCode(PlainSecret, code, It.IsAny<DateTime>())).Returns(step);

    [Fact(DisplayName = "Código válido deve ativar o MFA, registrar o step e commitar")]
    public async Task Execute_WithValidCode_ShouldEnableMfa()
    {
        var user = SetupUser(UserWithPendingSecret());
        SetupValidCode();

        await _sut.ExecuteAsync(user.Id, new EnableMfaDto("123456"));

        user.MfaEnabled.Should().BeTrue();
        user.MfaEnabledAt.Should().NotBeNull();
        user.LastUsedTotpStep.Should().Be(500); // o código do enable não pode ser reutilizado no verify
        _userRepo.Verify(r => r.UpdateAsync(user, It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "Deve devolver de 8 a 10 recovery codes distintos de ~10 caracteres")]
    public async Task Execute_ShouldReturnDistinctRecoveryCodes()
    {
        var user = SetupUser(UserWithPendingSecret());
        SetupValidCode();

        var result = await _sut.ExecuteAsync(user.Id, new EnableMfaDto("123456"));

        result.RecoveryCodes.Should().HaveCountGreaterOrEqualTo(8).And.HaveCountLessThanOrEqualTo(10);
        result.RecoveryCodes.Should().OnlyHaveUniqueItems();
        result.RecoveryCodes.Should().OnlyContain(c => c.Length >= 8 && c.Length <= 12);
    }

    [Fact(DisplayName = "Deve persistir apenas o hash dos recovery codes, nunca o código em claro")]
    public async Task Execute_ShouldPersistOnlyHashedRecoveryCodes()
    {
        var user = SetupUser(UserWithPendingSecret());
        SetupValidCode();
        List<(Guid UserId, string Hash)> saved = new();
        _recoveryRepo.Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<MfaRecoveryCode>>(), It.IsAny<CancellationToken>()))
            .Callback((IEnumerable<MfaRecoveryCode> codes, CancellationToken _) =>
                saved.AddRange(codes.Select(c => (c.UserId, c.CodeHash)))) // snapshot no callback
            .Returns(Task.CompletedTask);

        var result = await _sut.ExecuteAsync(user.Id, new EnableMfaDto("123456"));

        saved.Should().HaveCount(result.RecoveryCodes.Count);
        saved.Should().OnlyContain(s => s.UserId == user.Id);
        saved.Select(s => s.Hash).Should().BeEquivalentTo(result.RecoveryCodes.Select(c => "h:" + c));
        saved.Select(s => s.Hash).Should().NotIntersectWith(result.RecoveryCodes);
    }

    [Fact(DisplayName = "Deve invalidar recovery codes anteriores antes de gravar os novos")]
    public async Task Execute_ShouldRemoveOldRecoveryCodesBeforeAddingNew()
    {
        var user = SetupUser(UserWithPendingSecret());
        SetupValidCode();
        var sequence = new List<string>();
        _recoveryRepo.Setup(r => r.RemoveAllByUserIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .Callback(() => sequence.Add("remove")).Returns(Task.CompletedTask);
        _recoveryRepo.Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<MfaRecoveryCode>>(), It.IsAny<CancellationToken>()))
            .Callback(() => sequence.Add("add")).Returns(Task.CompletedTask);

        await _sut.ExecuteAsync(user.Id, new EnableMfaDto("123456"));

        sequence.Should().Equal("remove", "add");
    }

    [Fact(DisplayName = "Código errado não deve ativar, gerar recovery codes nem commitar")]
    public async Task Execute_WithWrongCode_ShouldNotEnable()
    {
        var user = SetupUser(UserWithPendingSecret());
        _totp.Setup(t => t.ValidateCode(PlainSecret, "000000", It.IsAny<DateTime>())).Returns((long?)null);

        var act = () => _sut.ExecuteAsync(user.Id, new EnableMfaDto("000000"));

        await act.Should().ThrowAsync<DomainException>();
        user.MfaEnabled.Should().BeFalse();
        user.LastUsedTotpStep.Should().BeNull();
        _recoveryRepo.Verify(r => r.AddRangeAsync(It.IsAny<IEnumerable<MfaRecoveryCode>>(), It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "Enable sem setup prévio deve lançar DomainException sem validar código")]
    public async Task Execute_WithoutSetup_ShouldThrow()
    {
        var user = SetupUser(User.Create("Caique", "caique@monkeybomb.com", "hashed_password"));

        var act = () => _sut.ExecuteAsync(user.Id, new EnableMfaDto("123456"));

        await act.Should().ThrowAsync<DomainException>();
        user.MfaEnabled.Should().BeFalse();
        _totp.Verify(t => t.ValidateCode(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>()), Times.Never);
        _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "Enable com MFA já ativo deve lançar DomainException sem regerar recovery codes")]
    public async Task Execute_WhenAlreadyEnabled_ShouldThrow()
    {
        var pending = UserWithPendingSecret();
        pending.EnableMfa(DateTime.UtcNow);
        var user = SetupUser(pending);
        SetupValidCode();

        var act = () => _sut.ExecuteAsync(user.Id, new EnableMfaDto("123456"));

        await act.Should().ThrowAsync<DomainException>();
        _recoveryRepo.Verify(r => r.AddRangeAsync(It.IsAny<IEnumerable<MfaRecoveryCode>>(), It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "Duplo submit: após sucesso o 2º Enable não deve gerar novos códigos nem commitar de novo")]
    public async Task Execute_DoubleSubmit_ShouldProcessOnlyOnce()
    {
        var user = SetupUser(UserWithPendingSecret());
        SetupValidCode();

        await _sut.ExecuteAsync(user.Id, new EnableMfaDto("123456"));
        var second = () => _sut.ExecuteAsync(user.Id, new EnableMfaDto("123456"));

        await second.Should().ThrowAsync<DomainException>();
        _recoveryRepo.Verify(r => r.AddRangeAsync(It.IsAny<IEnumerable<MfaRecoveryCode>>(), It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "Usuário inexistente deve lançar KeyNotFoundException")]
    public async Task Execute_WithUnknownUser_ShouldThrowKeyNotFound()
    {
        _userRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        var act = () => _sut.ExecuteAsync(Guid.NewGuid(), new EnableMfaDto("123456"));

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }
}
