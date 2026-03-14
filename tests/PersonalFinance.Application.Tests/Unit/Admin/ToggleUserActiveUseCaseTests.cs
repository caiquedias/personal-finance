using FluentAssertions;
using Moq;
using PersonalFinance.Application.UseCases.Admin;
using PersonalFinance.Domain.Entities.Auth;
using PersonalFinance.Domain.Exceptions;
using PersonalFinance.Domain.Interfaces.Repositories;
using Xunit;

namespace PersonalFinance.Application.Tests.Unit.Admin;

public class ToggleUserActiveUseCaseTests
{
    private readonly Mock<IAdminUserRepository> _userRepo = new();
    private readonly Mock<IUnitOfWork>          _uow      = new();
    private readonly ToggleUserActiveUseCase    _sut;

    private static readonly Guid AdminId = Guid.NewGuid();

    public ToggleUserActiveUseCaseTests()
    {
        _sut = new ToggleUserActiveUseCase(_userRepo.Object, _uow.Object);
    }

    [Fact(DisplayName = "Deve desativar usuário ativo")]
    public async Task Execute_WithActiveUser_ShouldDeactivate()
    {
        var targetId = Guid.NewGuid();
        var user     = User.Create("Target", "target@x.com", "hash");

        _userRepo.Setup(r => r.GetByIdAsync(targetId, default)).ReturnsAsync(user);

        await _sut.ExecuteAsync(targetId, AdminId);

        user.IsActive.Should().BeFalse();
        user.IsDeleted.Should().BeTrue();
        _uow.Verify(u => u.CommitAsync(default), Times.Once);
    }

    [Fact(DisplayName = "Deve reativar usuário inativo")]
    public async Task Execute_WithInactiveUser_ShouldReactivate()
    {
        var targetId = Guid.NewGuid();
        var user     = User.Create("Target", "target@x.com", "hash");
        user.SoftDelete();

        _userRepo.Setup(r => r.GetByIdAsync(targetId, default)).ReturnsAsync(user);

        await _sut.ExecuteAsync(targetId, AdminId);

        user.IsActive.Should().BeTrue();
        user.IsDeleted.Should().BeFalse();
        _uow.Verify(u => u.CommitAsync(default), Times.Once);
    }

    [Fact(DisplayName = "Não deve permitir admin desativar a si próprio")]
    public async Task Execute_AdminTargetingSelf_ShouldThrow()
    {
        var act = () => _sut.ExecuteAsync(AdminId, AdminId);

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

        var act = () => _sut.ExecuteAsync(targetId, AdminId);

        await act.Should().ThrowAsync<KeyNotFoundException>();
        _uow.Verify(u => u.CommitAsync(default), Times.Never);
    }
}
