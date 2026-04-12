using FluentAssertions;
using Moq;
using PersonalFinance.Application.DTOs.Admin;
using PersonalFinance.Application.UseCases.Admin;
using PersonalFinance.Domain.Entities.Auth;
using PersonalFinance.Domain.Interfaces.Repositories;
using Xunit;

namespace PersonalFinance.Application.Tests.Unit.Admin;

public class GetUsersUseCaseTests
{
    private readonly Mock<IAdminUserRepository> _userRepo = new();
    private readonly Mock<IUserRoleRepository>  _roleRepo = new();
    private readonly GetUsersUseCase            _sut;

    public GetUsersUseCaseTests()
    {
        _sut = new GetUsersUseCase(_userRepo.Object, _roleRepo.Object);
    }

    [Fact(DisplayName = "Deve retornar lista de usuários com roles")]
    public async Task Execute_ShouldReturnAllUsersWithRoles()
    {
        var user1 = User.Create("Caique", "caique@x.com", "hash1");
        var user2 = User.Create("Admin",  "admin@x.com",  "hash2");

        _userRepo.Setup(r => r.GetPagedAsync(1, 20, null, null, null, default))
                 .ReturnsAsync((new[] { user1, user2 }.AsEnumerable(), 2));
        _roleRepo.Setup(r => r.GetRoleNamesByUserIdAsync(user1.Id, default))
                 .ReturnsAsync(new[] { "User" });
        _roleRepo.Setup(r => r.GetRoleNamesByUserIdAsync(user2.Id, default))
                 .ReturnsAsync(new[] { "Admin", "User" });

        var result = (await _sut.ExecuteAsync(new AdminUserFilterDto())).Items.ToList();

        result.Should().HaveCount(2);
        result[0].Roles.Should().Contain("User");
        result[1].Roles.Should().Contain("Admin");
    }

    [Fact(DisplayName = "Deve retornar lista vazia se não há usuários")]
    public async Task Execute_WithNoUsers_ShouldReturnEmptyList()
    {
        _userRepo.Setup(r => r.GetPagedAsync(1, 20, null, null, null, default))
                 .ReturnsAsync((Enumerable.Empty<User>(), 0));

        var result = await _sut.ExecuteAsync(new AdminUserFilterDto());

        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    [Fact(DisplayName = "Deve buscar roles para cada usuário individualmente")]
    public async Task Execute_ShouldFetchRolesPerUser()
    {
        var user1 = User.Create("A", "a@x.com", "hash");
        var user2 = User.Create("B", "b@x.com", "hash");

        _userRepo.Setup(r => r.GetPagedAsync(1, 20, null, null, null, default))
                 .ReturnsAsync((new[] { user1, user2 }.AsEnumerable(), 2));
        _roleRepo.Setup(r => r.GetRoleNamesByUserIdAsync(It.IsAny<Guid>(), default))
                 .ReturnsAsync(Array.Empty<string>());

        await _sut.ExecuteAsync(new AdminUserFilterDto());

        _roleRepo.Verify(r => r.GetRoleNamesByUserIdAsync(user1.Id, default), Times.Once);
        _roleRepo.Verify(r => r.GetRoleNamesByUserIdAsync(user2.Id, default), Times.Once);
    }

    [Fact(DisplayName = "Deve retornar dados corretos do DTO")]
    public async Task Execute_ShouldMapUserFieldsCorrectly()
    {
        var user = User.Create("Caique Dias", "caique@monkeybomb.com", "hash");

        _userRepo.Setup(r => r.GetPagedAsync(1, 20, null, null, null, default))
                 .ReturnsAsync((new[] { user }.AsEnumerable(), 1));
        _roleRepo.Setup(r => r.GetRoleNamesByUserIdAsync(user.Id, default))
                 .ReturnsAsync(new[] { "User" });

        var result = (await _sut.ExecuteAsync(new AdminUserFilterDto())).Items.First();

        result.Id.Should().Be(user.Id);
        result.Name.Should().Be("Caique Dias");
        result.Email.Should().Be("caique@monkeybomb.com");
        result.IsActive.Should().BeTrue();
    }
}
