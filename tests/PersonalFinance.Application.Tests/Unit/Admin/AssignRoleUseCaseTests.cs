using FluentAssertions;
using Moq;
using PersonalFinance.Application.DTOs.Admin;
using PersonalFinance.Application.UseCases.Admin;
using PersonalFinance.Domain.Entities.Auth;
using PersonalFinance.Domain.Exceptions;
using PersonalFinance.Domain.Interfaces.Repositories;
using Xunit;

namespace PersonalFinance.Application.Tests.Unit.Admin;

public class AssignRoleUseCaseTests
{
    private readonly Mock<IAdminUserRepository> _userRepo = new();
    private readonly Mock<IUserRoleRepository>  _roleRepo = new();
    private readonly Mock<IUnitOfWork>          _uow      = new();
    private readonly AssignRoleUseCase          _sut;

    public AssignRoleUseCaseTests()
    {
        _sut = new AssignRoleUseCase(_userRepo.Object, _roleRepo.Object, _uow.Object);
    }

    [Fact(DisplayName = "Deve atribuir role a usuário ativo")]
    public async Task Execute_WithValidData_ShouldAssignRole()
    {
        var userId = Guid.NewGuid();
        var user   = User.Create("Caique", "caique@x.com", "hash");

        _userRepo.Setup(r => r.GetByIdAsync(userId, default)).ReturnsAsync(user);
        _roleRepo.Setup(r => r.UserHasRoleAsync(userId, 1, default)).ReturnsAsync(false);

        await _sut.ExecuteAsync(new AssignRoleDto(userId, 1));

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

        var act = () => _sut.ExecuteAsync(new AssignRoleDto(userId, 1));

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

        var act = () => _sut.ExecuteAsync(new AssignRoleDto(userId, 1));

        await act.Should().ThrowAsync<DomainException>().WithMessage("*inativo*");
        _uow.Verify(u => u.CommitAsync(default), Times.Never);
    }

    [Fact(DisplayName = "Deve lançar exceção para usuário não encontrado")]
    public async Task Execute_WithNotFoundUser_ShouldThrow()
    {
        var userId = Guid.NewGuid();
        _userRepo.Setup(r => r.GetByIdAsync(userId, default)).ReturnsAsync((User?)null);

        var act = () => _sut.ExecuteAsync(new AssignRoleDto(userId, 1));

        await act.Should().ThrowAsync<KeyNotFoundException>();
        _uow.Verify(u => u.CommitAsync(default), Times.Never);
    }
}
