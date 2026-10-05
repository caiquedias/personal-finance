using FluentAssertions;
using Moq;
using PersonalFinance.Application.DTOs.Admin;
using PersonalFinance.Application.UseCases.Admin;
using PersonalFinance.Domain.Entities.Auth;
using PersonalFinance.Domain.Interfaces.Repositories;
using FluentValidation;
using PersonalFinance.Application.Tests.Unit.Support;
using Xunit;

namespace PersonalFinance.Application.Tests.Unit.Admin;

public class UpdateUserByAdminUseCaseTests
{
    private readonly Mock<IAdminUserRepository> _userRepo = new();
    private readonly Mock<IUserRoleRepository>  _roleRepo = new();
    private readonly Mock<IUnitOfWork>          _uow      = new();
    private readonly UpdateUserByAdminUseCase   _sut;

    public UpdateUserByAdminUseCaseTests()
    {
        _sut = new UpdateUserByAdminUseCase(_userRepo.Object, _roleRepo.Object, _uow.Object);
    }

    [Fact(DisplayName = "Deve atualizar nome do usuário")]
    public async Task Execute_WithExistingUser_ShouldUpdateName()
    {
        var userId = Guid.NewGuid();
        var user   = User.Create("Antigo", "caique@x.com", "hash");
        var dto    = new UpdateUserByAdminDto(userId, "Novo Nome");

        _userRepo.Setup(r => r.GetByIdAsync(userId, default)).ReturnsAsync(user);
        _roleRepo.Setup(r => r.GetRoleNamesByUserIdAsync(user.Id, default)).ReturnsAsync(["User"]);

        var result = await _sut.ExecuteAsync(dto);

        result.Name.Should().Be("Novo Nome");
        _userRepo.Verify(r => r.UpdateAsync(user, default), Times.Once);
        _uow.Verify(u => u.CommitAsync(default), Times.Once);
    }

    [Fact(DisplayName = "Deve espelhar MfaEnabled=true no retorno da atualização")]
    public async Task Execute_WithMfaEnabledUser_ShouldMirrorMfaEnabled()
    {
        var user = User.Create("Antigo", "caique@x.com", "hash");
        user.SetPendingMfaSecret("secret-cifrado");
        user.EnableMfa(DateTime.UtcNow);
        _userRepo.Setup(r => r.GetByIdAsync(user.Id, default)).ReturnsAsync(user);
        _roleRepo.Setup(r => r.GetRoleNamesByUserIdAsync(user.Id, default)).ReturnsAsync(["User"]);

        var result = await _sut.ExecuteAsync(new UpdateUserByAdminDto(user.Id, "Novo"));

        result.Should().BeEquivalentTo(new { MfaEnabled = true, MfaSetupPending = false });
    }

    [Fact(DisplayName = "Deve espelhar MfaSetupPending=true no retorno da atualização")]
    public async Task Execute_WithPendingMfaSecret_ShouldMirrorMfaSetupPending()
    {
        var user = User.Create("Antigo", "caique@x.com", "hash");
        user.SetPendingMfaSecret("secret-cifrado");
        _userRepo.Setup(r => r.GetByIdAsync(user.Id, default)).ReturnsAsync(user);
        _roleRepo.Setup(r => r.GetRoleNamesByUserIdAsync(user.Id, default)).ReturnsAsync(["User"]);

        var result = await _sut.ExecuteAsync(new UpdateUserByAdminDto(user.Id, "Novo"));

        result.Should().BeEquivalentTo(new { MfaEnabled = false, MfaSetupPending = true });
    }

    [Fact(DisplayName = "Deve lançar exceção se usuário não encontrado")]
    public async Task Execute_WithNotFoundUser_ShouldThrow()
    {
        var dto = new UpdateUserByAdminDto(Guid.NewGuid(), "Novo Nome");
        _userRepo.Setup(r => r.GetByIdAsync(dto.UserId, default)).ReturnsAsync((User?)null);

        var act = () => _sut.ExecuteAsync(dto);

        await act.Should().ThrowAsync<KeyNotFoundException>();
        _uow.Verify(u => u.CommitAsync(default), Times.Never);
    }

    // ── Validação (#396) ──────────────────────────────────────────────────────

    [Fact(DisplayName = "Deve lançar ValidationException e não persistir quando o validator reprova")]
    public async Task Execute_WhenValidatorFails_ShouldThrowValidationExceptionWithoutPersisting()
    {
        var sut = UseCaseFactory.Create<UpdateUserByAdminUseCase>(
            _userRepo.Object, _roleRepo.Object, _uow.Object,
            TestValidators.Invalid<UpdateUserByAdminDto>());

        var act = () => sut.ExecuteAsync(new UpdateUserByAdminDto(Guid.NewGuid(), "Novo"));

        await act.Should().ThrowAsync<ValidationException>();
        _userRepo.Invocations.Should().BeEmpty();
        _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
