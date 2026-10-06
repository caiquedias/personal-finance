using FluentAssertions;
using FluentValidation;
using Moq;
using PersonalFinance.Application.DTOs.Auth;
using PersonalFinance.Application.Tests.Unit.Support;
using PersonalFinance.Application.UseCases.Auth;
using PersonalFinance.Domain.Entities.Auth;
using PersonalFinance.Domain.Enums;
using PersonalFinance.Domain.Exceptions;
using PersonalFinance.Domain.Interfaces.Repositories;
using Xunit;

namespace PersonalFinance.Application.Tests.Unit.Auth;

/// <summary>
/// RequestPasswordResetUseCase (#404): anti-enumeração (sem exceção e sem efeito para e-mail inexistente,
/// inativo ou removido), emissão do código, auditoria PasswordResetRequested só quando o usuário existe.
/// </summary>
public class RequestPasswordResetUseCaseTests
{
    private const string Ip = "203.0.113.7";

    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<IAuditLogRepository> _audit = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly IssuerHarness _h;
    private readonly AuditTrace _trace;

    public RequestPasswordResetUseCaseTests()
    {
        _h = new IssuerHarness(_uow);
        _trace = AuditTrace.Track(_audit, _uow);
    }

    private RequestPasswordResetUseCase Sut(IValidator<EmailRequestDto>? validator = null) =>
        UseCaseFactory.Create<RequestPasswordResetUseCase>(
            _userRepo.Object, _h.Issuer, _audit.Object, _uow.Object,
            validator ?? TestValidators.Valid<EmailRequestDto>());

    private User ExistingUser()
    {
        var user = User.Create("Ana", "ana@x.com", "hash");
        _userRepo.Setup(r => r.GetByEmailAsync("ana@x.com", It.IsAny<CancellationToken>())).ReturnsAsync(user);
        return user;
    }

    [Fact(DisplayName = "Usuário ativo: emite o token de reset e enfileira o e-mail com o código")]
    public async Task Execute_ExistingUser_ShouldIssueResetAndEnqueue()
    {
        var user = ExistingUser();

        await Sut().ExecuteAsync(new EmailRequestDto("ana@x.com"), Ip);

        _h.Added.Should().ContainSingle().Which.Purpose.Should().Be(UserTokenPurpose.PasswordReset);
        _h.Added[0].UserId.Should().Be(user.Id);
        var message = _h.Enqueued.Should().ContainSingle().Subject;
        message.To.Should().Be("ana@x.com");
        message.HtmlBody.Should().Contain(IssuerHarness.Code);
        message.HtmlBody.Should().Contain("/reset-password#email=ana%40x.com");
    }

    [Fact(DisplayName = "E-mail é normalizado (trim + lowercase) antes da busca")]
    public async Task Execute_ShouldNormalizeEmail()
    {
        ExistingUser();

        await Sut().ExecuteAsync(new EmailRequestDto("  ANA@X.com "), Ip);

        _userRepo.Verify(r => r.GetByEmailAsync("ana@x.com", It.IsAny<CancellationToken>()), Times.Once);
        _h.Enqueued.Should().ContainSingle();
    }

    [Fact(DisplayName = "Audita PasswordResetRequested com ator = alvo = o próprio usuário, IP e ANTES do commit")]
    public async Task Execute_ExistingUser_ShouldAuditRequestedInSameTransaction()
    {
        var user = ExistingUser();

        await Sut().ExecuteAsync(new EmailRequestDto("ana@x.com"), Ip);

        var log = _trace.Logs.Should().ContainSingle().Subject;
        log.Action.Should().Be(AuditAction.PasswordResetRequested);
        log.ActorUserId.Should().Be(user.Id);
        log.TargetUserId.Should().Be(user.Id);
        log.IpAddress.Should().Be(Ip);
        _trace.Order.Should().Equal("audit", "commit");
    }

    [Fact(DisplayName = "Auditoria não contém código, e-mail nem hash")]
    public async Task Execute_Audit_ShouldNotLeakSecrets()
    {
        ExistingUser();

        await Sut().ExecuteAsync(new EmailRequestDto("ana@x.com"), Ip);

        var details = _trace.Logs.Single().Details ?? string.Empty;
        details.Should().NotContain(IssuerHarness.Code).And.NotContain("ana@x.com").And.NotContain(IssuerHarness.ComputedHash);
    }

    [Fact(DisplayName = "E-mail inexistente: não lança, não emite, não enfileira, não audita, não commita")]
    public async Task Execute_UnknownEmail_ShouldDoNothingSilently()
    {
        _userRepo.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        var act = () => Sut().ExecuteAsync(new EmailRequestDto("ghost@x.com"), Ip);

        await act.Should().NotThrowAsync();
        _h.Enqueued.Should().BeEmpty();
        _h.Added.Should().BeEmpty();
        _trace.Logs.Should().BeEmpty();
        _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "Usuário inativo: nenhum token, nenhum e-mail, nenhuma auditoria")]
    public async Task Execute_InactiveUser_ShouldDoNothing()
    {
        var user = ExistingUser();
        user.Deactivate();

        var act = () => Sut().ExecuteAsync(new EmailRequestDto("ana@x.com"), Ip);

        await act.Should().NotThrowAsync();
        _h.Enqueued.Should().BeEmpty();
        _h.Added.Should().BeEmpty();
        _trace.Logs.Should().BeEmpty();
    }

    [Fact(DisplayName = "Usuário removido (soft delete): nenhum token nem e-mail")]
    public async Task Execute_DeletedUser_ShouldDoNothing()
    {
        var user = ExistingUser();
        user.SoftDelete();

        var act = () => Sut().ExecuteAsync(new EmailRequestDto("ana@x.com"), Ip);

        await act.Should().NotThrowAsync();
        _h.Enqueued.Should().BeEmpty();
        _h.Added.Should().BeEmpty();
        _trace.Logs.Should().BeEmpty();
    }

    [Fact(DisplayName = "Dentro do cooldown: não lança e não enfileira um segundo e-mail")]
    public async Task Execute_WithinCooldown_ShouldNotEnqueueAgain()
    {
        var user = ExistingUser();
        _h.SetupLatestToken(user, UserTokenPurpose.PasswordReset, TimeSpan.FromSeconds(5));

        var act = () => Sut().ExecuteAsync(new EmailRequestDto("ana@x.com"), Ip);

        await act.Should().NotThrowAsync();
        _h.Enqueued.Should().BeEmpty();
        _h.Added.Should().BeEmpty();
    }

    [Fact(DisplayName = "Corrida (conflito de concorrência no commit): não lança e não enfileira — resposta idêntica ao inexistente")]
    public async Task Execute_WhenCommitConflicts_ShouldNotThrowNorEnqueue()
    {
        ExistingUser();
        _uow.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConcurrencyConflictException());

        var act = () => Sut().ExecuteAsync(new EmailRequestDto("ana@x.com"), Ip);

        await act.Should().NotThrowAsync();
        _h.Enqueued.Should().BeEmpty();
    }

    [Fact(DisplayName = "Validator reprovado: lança ValidationException sem tocar repositório, fila ou commit")]
    public async Task Execute_WhenValidatorFails_ShouldThrowAndTouchNothing()
    {
        var sut = Sut(TestValidators.Invalid<EmailRequestDto>());

        await Assert.ThrowsAsync<ValidationException>(() => sut.ExecuteAsync(new EmailRequestDto("ana@x.com"), Ip));

        _userRepo.Invocations.Should().BeEmpty();
        _h.Enqueued.Should().BeEmpty();
        _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
