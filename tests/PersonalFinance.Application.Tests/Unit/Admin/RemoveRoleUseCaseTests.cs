using FluentAssertions;
using Moq;
using PersonalFinance.Application.DTOs.Admin;
using PersonalFinance.Application.UseCases.Admin;
using PersonalFinance.Domain.Exceptions;
using PersonalFinance.Domain.Interfaces.Repositories;
using Xunit;

namespace PersonalFinance.Application.Tests.Unit.Admin;

public class RemoveRoleUseCaseTests
{
    private readonly Mock<IUserRoleRepository> _roleRepo = new();
    private readonly Mock<IUnitOfWork>         _uow      = new();
    private readonly RemoveRoleUseCase         _sut;

    private static readonly Guid AdminId = Guid.NewGuid();

    public RemoveRoleUseCaseTests()
    {
        _sut = new RemoveRoleUseCase(_roleRepo.Object, _uow.Object);
    }

    [Fact(DisplayName = "Deve remover role de outro usuário")]
    public async Task Execute_WithValidData_ShouldRemoveRole()
    {
        var targetId = Guid.NewGuid();
        _roleRepo.Setup(r => r.UserHasRoleAsync(targetId, 2, default)).ReturnsAsync(true);

        await _sut.ExecuteAsync(new RemoveRoleDto(targetId, 2), AdminId);

        _roleRepo.Verify(r => r.RemoveAsync(targetId, 2, default), Times.Once);
        _uow.Verify(u => u.CommitAsync(default), Times.Once);
    }

    [Fact(DisplayName = "Não deve permitir admin remover a própria role Admin")]
    public async Task Execute_AdminRemovingOwnAdminRole_ShouldThrow()
    {
        var act = () => _sut.ExecuteAsync(new RemoveRoleDto(AdminId, 1), AdminId);

        await act.Should().ThrowAsync<DomainException>()
                 .WithMessage("*própria role Admin*");
        _uow.Verify(u => u.CommitAsync(default), Times.Never);
    }

    [Fact(DisplayName = "Deve lançar exceção se usuário não possui a role")]
    public async Task Execute_WithRoleNotOwned_ShouldThrow()
    {
        var targetId = Guid.NewGuid();
        _roleRepo.Setup(r => r.UserHasRoleAsync(targetId, 2, default)).ReturnsAsync(false);

        var act = () => _sut.ExecuteAsync(new RemoveRoleDto(targetId, 2), AdminId);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*não possui*");
        _uow.Verify(u => u.CommitAsync(default), Times.Never);
    }

    [Fact(DisplayName = "Admin pode remover role Admin de outro usuário")]
    public async Task Execute_AdminRemovingOtherUsersAdminRole_ShouldSucceed()
    {
        var otherId = Guid.NewGuid();
        _roleRepo.Setup(r => r.UserHasRoleAsync(otherId, 1, default)).ReturnsAsync(true);

        await _sut.ExecuteAsync(new RemoveRoleDto(otherId, 1), AdminId);

        _roleRepo.Verify(r => r.RemoveAsync(otherId, 1, default), Times.Once);
        _uow.Verify(u => u.CommitAsync(default), Times.Once);
    }
}
