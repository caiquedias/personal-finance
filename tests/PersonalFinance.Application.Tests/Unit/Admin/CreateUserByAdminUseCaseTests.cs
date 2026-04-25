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

public class CreateUserByAdminUseCaseTests
{
    private readonly Mock<IAdminUserRepository> _userRepo = new();
    private readonly Mock<IUserRoleRepository>  _roleRepo = new();
    private readonly Mock<IPasswordHasher>      _hasher   = new();
    private readonly Mock<IUnitOfWork>          _uow      = new();
    private readonly CreateUserByAdminUseCase   _sut;

    public CreateUserByAdminUseCaseTests()
    {
        _sut = new CreateUserByAdminUseCase(_userRepo.Object, _roleRepo.Object, _hasher.Object, _uow.Object);
    }

    [Fact(DisplayName = "Deve criar usuário e atribuir role User padrão")]
    public async Task Execute_WithValidData_ShouldCreateUser()
    {
        var dto = new CreateUserByAdminDto("Caique", "caique@x.com", "senha123");
        _userRepo.Setup(r => r.ExistsByEmailAsync(dto.Email, default)).ReturnsAsync(false);
        _hasher.Setup(h => h.Hash(dto.Password)).Returns("hash");

        var result = await _sut.ExecuteAsync(dto);

        result.Name.Should().Be("Caique");
        result.Email.Should().Be("caique@x.com");
        result.Roles.Should().Contain("User");
        _userRepo.Verify(r => r.AddAsync(It.IsAny<User>(), default), Times.Once);
        _roleRepo.Verify(r => r.AssignAsync(It.Is<UserRole>(ur => ur.RoleId == 2), default), Times.Once);
        _uow.Verify(u => u.CommitAsync(default), Times.Once);
    }

    [Fact(DisplayName = "Deve lançar exceção se e-mail já existe")]
    public async Task Execute_WithDuplicateEmail_ShouldThrow()
    {
        var dto = new CreateUserByAdminDto("Caique", "caique@x.com", "senha123");
        _userRepo.Setup(r => r.ExistsByEmailAsync(dto.Email, default)).ReturnsAsync(true);

        var act = () => _sut.ExecuteAsync(dto);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*e-mail*");
        _uow.Verify(u => u.CommitAsync(default), Times.Never);
    }

    [Theory(DisplayName = "Deve lançar exceção para senha com menos de 8 caracteres")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("123")]
    public async Task Execute_WithShortPassword_ShouldThrow(string password)
    {
        var dto = new CreateUserByAdminDto("Caique", "caique@x.com", password);

        var act = () => _sut.ExecuteAsync(dto);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*8 caracteres*");
        _uow.Verify(u => u.CommitAsync(default), Times.Never);
    }
}
