using FluentAssertions;
using Moq;
using PersonalFinance.Application.DTOs.Admin;
using PersonalFinance.Application.UseCases.Admin;
using PersonalFinance.Domain.Entities.Auth;
using PersonalFinance.Domain.Exceptions;
using PersonalFinance.Domain.Interfaces.Repositories;
using PersonalFinance.Domain.Interfaces.Services;
using Xunit;

namespace PersonalFinance.Application.Tests.Unit.Admin;

public class ResetUserPasswordUseCaseTests
{
    private readonly Mock<IAdminUserRepository> _userRepo = new();
    private readonly Mock<IPasswordHasher>      _hasher   = new();
    private readonly Mock<IUnitOfWork>          _uow      = new();
    private readonly ResetUserPasswordUseCase   _sut;

    private static readonly Guid AdminId = Guid.NewGuid();

    public ResetUserPasswordUseCaseTests()
    {
        _sut = new ResetUserPasswordUseCase(_userRepo.Object, _hasher.Object, _uow.Object);
    }

    [Fact(DisplayName = "Deve resetar senha de outro usuário")]
    public async Task Execute_WithValidData_ShouldUpdatePasswordHash()
    {
        var targetId = Guid.NewGuid();
        var user     = User.Create("Target", "target@x.com", "old_hash");

        _userRepo.Setup(r => r.GetByIdAsync(targetId, default)).ReturnsAsync(user);
        _hasher.Setup(h => h.Hash("NovaSenha@123")).Returns("new_hash");

        await _sut.ExecuteAsync(new ResetPasswordDto(targetId, "NovaSenha@123"), AdminId);

        user.PasswordHash.Should().Be("new_hash");
        _uow.Verify(u => u.CommitAsync(default), Times.Once);
    }

    [Fact(DisplayName = "Deve chamar o hasher antes de atualizar")]
    public async Task Execute_ShouldHashPasswordBeforeUpdate()
    {
        var targetId = Guid.NewGuid();
        var user     = User.Create("Target", "target@x.com", "old_hash");

        _userRepo.Setup(r => r.GetByIdAsync(targetId, default)).ReturnsAsync(user);
        _hasher.Setup(h => h.Hash("NovaSenha@123")).Returns("hashed");

        await _sut.ExecuteAsync(new ResetPasswordDto(targetId, "NovaSenha@123"), AdminId);

        _hasher.Verify(h => h.Hash("NovaSenha@123"), Times.Once);
    }

    [Fact(DisplayName = "Não deve permitir admin resetar a própria senha por este endpoint")]
    public async Task Execute_AdminResettingOwnPassword_ShouldThrow()
    {
        var act = () => _sut.ExecuteAsync(
            new ResetPasswordDto(AdminId, "NovaSenha@123"), AdminId);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*perfil*");
        _uow.Verify(u => u.CommitAsync(default), Times.Never);
    }

    [Fact(DisplayName = "Deve lançar exceção para senha menor que 8 caracteres")]
    public async Task Execute_WithShortPassword_ShouldThrow()
    {
        var targetId = Guid.NewGuid();

        var act = () => _sut.ExecuteAsync(
            new ResetPasswordDto(targetId, "abc"), AdminId);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*8*");
        _uow.Verify(u => u.CommitAsync(default), Times.Never);
    }

    [Fact(DisplayName = "Deve lançar exceção para usuário não encontrado")]
    public async Task Execute_WithNotFoundUser_ShouldThrow()
    {
        var targetId = Guid.NewGuid();
        _userRepo.Setup(r => r.GetByIdAsync(targetId, default)).ReturnsAsync((User?)null);

        var act = () => _sut.ExecuteAsync(
            new ResetPasswordDto(targetId, "NovaSenha@123"), AdminId);

        await act.Should().ThrowAsync<KeyNotFoundException>();
        _uow.Verify(u => u.CommitAsync(default), Times.Never);
    }
}
