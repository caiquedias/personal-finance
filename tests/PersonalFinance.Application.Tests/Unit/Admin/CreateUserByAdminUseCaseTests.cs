using FluentAssertions;
using Moq;
using PersonalFinance.Application.DTOs.Admin;
using PersonalFinance.Application.UseCases.Admin;
using PersonalFinance.Domain.Entities.Auth;
using PersonalFinance.Domain.Exceptions;
using PersonalFinance.Domain.Interfaces.Repositories;
using PersonalFinance.Domain.Interfaces.Services;
using FluentValidation;
using PersonalFinance.Application.Tests.Unit.Support;
using PersonalFinance.Domain.Enums;
using System.Text.Json;
using Xunit;

namespace PersonalFinance.Application.Tests.Unit.Admin;

public class CreateUserByAdminUseCaseTests
{
    private readonly Mock<IAdminUserRepository> _userRepo = new();
    private readonly Mock<IUserRoleRepository>  _roleRepo = new();
    private readonly Mock<IPasswordHasher>      _hasher   = new();
    private readonly Mock<IUnitOfWork>          _uow      = new();
    private readonly Mock<IAuditLogRepository> _audit = new();
    private const string Ip = "203.0.113.7";
    private static readonly Guid AdminId = Guid.NewGuid();
    private readonly CreateUserByAdminUseCase   _sut;

    public CreateUserByAdminUseCaseTests()
    {
        _sut = UseCaseFactory.Create<CreateUserByAdminUseCase>(_userRepo.Object, _roleRepo.Object, _hasher.Object, _uow.Object, TestValidators.Valid<CreateUserByAdminDto>(), _audit.Object);
    }

    [Fact(DisplayName = "Deve criar usuário e atribuir role User padrão")]
    public async Task Execute_WithValidData_ShouldCreateUser()
    {
        var dto = new CreateUserByAdminDto("Caique", "caique@x.com", "senha123");
        _userRepo.Setup(r => r.ExistsByEmailAsync(dto.Email, default)).ReturnsAsync(false);
        _hasher.Setup(h => h.Hash(dto.Password)).Returns("hash");

        var result = await _sut.ExecuteAsync(dto, AdminId, Ip);

        result.Name.Should().Be("Caique");
        result.Email.Should().Be("caique@x.com");
        result.Roles.Should().Contain("User");
        _userRepo.Verify(r => r.AddAsync(It.IsAny<User>(), default), Times.Once);
        _roleRepo.Verify(r => r.AssignAsync(It.Is<UserRole>(ur => ur.RoleId == 2), default), Times.Once);
        _uow.Verify(u => u.CommitAsync(default), Times.Once);
    }

    [Fact(DisplayName = "Deve retornar MFA false/false para usuário recém-criado")]
    public async Task Execute_WithValidData_ShouldReturnMfaFlagsFalse()
    {
        var dto = new CreateUserByAdminDto("Caique", "caique@x.com", "senha123");
        _userRepo.Setup(r => r.ExistsByEmailAsync(dto.Email, default)).ReturnsAsync(false);
        _hasher.Setup(h => h.Hash(dto.Password)).Returns("hash");

        var result = await _sut.ExecuteAsync(dto, AdminId, Ip);

        result.Should().BeEquivalentTo(new { MfaEnabled = false, MfaSetupPending = false });
    }

    [Fact(DisplayName = "Deve lançar exceção se e-mail já existe")]
    public async Task Execute_WithDuplicateEmail_ShouldThrow()
    {
        var dto = new CreateUserByAdminDto("Caique", "caique@x.com", "senha123");
        _userRepo.Setup(r => r.ExistsByEmailAsync(dto.Email, default)).ReturnsAsync(true);

        var act = () => _sut.ExecuteAsync(dto, AdminId, Ip);

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

        var act = () => _sut.ExecuteAsync(dto, AdminId, Ip);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*8 caracteres*");
        _uow.Verify(u => u.CommitAsync(default), Times.Never);
    }

    // ── Validação (#396) ──────────────────────────────────────────────────────

    private CreateUserByAdminUseCase SutWith(IValidator<CreateUserByAdminDto> validator) =>
        UseCaseFactory.Create<CreateUserByAdminUseCase>(
            _userRepo.Object, _roleRepo.Object, _hasher.Object, _uow.Object, _audit.Object, validator);

    [Fact(DisplayName = "Deve lançar ValidationException e não persistir quando o validator reprova")]
    public async Task Execute_WhenValidatorFails_ShouldThrowValidationExceptionWithoutPersisting()
    {
        var sut = SutWith(TestValidators.Invalid<CreateUserByAdminDto>());

        var act = () => sut.ExecuteAsync(new CreateUserByAdminDto("Novo", "novo@x.com", "Senha@123"), AdminId, Ip);

        await act.Should().ThrowAsync<ValidationException>();
        _userRepo.Verify(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Auditoria (#402) ──────────────────────────────────────────────────────

    private void VerifyNoAudit() =>
        _audit.Verify(a => a.AddAsync(It.IsAny<AuditLog>(), It.IsAny<CancellationToken>()), Times.Never);

    [Fact(DisplayName = "Criar usuário deve gravar auditoria UserCreated (alvo = novo usuário) sem senha/hash/e-mail")]
    public async Task Execute_WithValidData_ShouldAuditBeforeCommit()
    {
        var dto = new CreateUserByAdminDto("Caique", "caique@x.com", "senha1234");
        _userRepo.Setup(r => r.ExistsByEmailAsync(dto.Email, default)).ReturnsAsync(false);
        _hasher.Setup(h => h.Hash(dto.Password)).Returns("hash-secreto");
        var trace = AuditTrace.Track(_audit, _uow);

        var result = await _sut.ExecuteAsync(dto, AdminId, Ip);

        var log = trace.Logs.Should().ContainSingle().Subject;
        log.Action.Should().Be(AuditAction.UserCreated);
        log.ActorUserId.Should().Be(AdminId);
        log.TargetUserId.Should().Be(result.Id);
        log.IpAddress.Should().Be(Ip);
        (log.Details ?? string.Empty).Should().NotContain("senha1234")
            .And.NotContain("hash-secreto").And.NotContain("caique@x.com");
        trace.Order.IndexOf("audit").Should().BeGreaterThanOrEqualTo(0);
        trace.Order.IndexOf("audit").Should().BeLessThan(trace.Order.IndexOf("commit"));
    }

    [Fact(DisplayName = "Não deve auditar com e-mail duplicado, senha curta ou validator reprovado")]
    public async Task Execute_ErrorPaths_ShouldNotAudit()
    {
        _userRepo.Setup(r => r.ExistsByEmailAsync("dup@x.com", default)).ReturnsAsync(true);

        await ((Func<Task>)(() => _sut.ExecuteAsync(new CreateUserByAdminDto("A", "dup@x.com", "senha1234"), AdminId, Ip)))
            .Should().ThrowAsync<DomainException>();
        await ((Func<Task>)(() => _sut.ExecuteAsync(new CreateUserByAdminDto("A", "a@x.com", "123"), AdminId, Ip)))
            .Should().ThrowAsync<DomainException>();
        await ((Func<Task>)(() => SutWith(TestValidators.Invalid<CreateUserByAdminDto>())
                .ExecuteAsync(new CreateUserByAdminDto("A", "a@x.com", "senha1234"), AdminId, Ip)))
            .Should().ThrowAsync<ValidationException>();

        VerifyNoAudit();
    }
}
