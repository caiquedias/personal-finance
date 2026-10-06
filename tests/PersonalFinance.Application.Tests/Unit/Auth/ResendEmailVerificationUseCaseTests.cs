using FluentAssertions;
using FluentValidation;
using Moq;
using PersonalFinance.Application.DTOs.Auth;
using PersonalFinance.Application.Tests.Unit.Support;
using PersonalFinance.Application.UseCases.Auth;
using PersonalFinance.Domain.Entities.Auth;
using PersonalFinance.Domain.Enums;
using PersonalFinance.Domain.Interfaces.Repositories;
using Xunit;

namespace PersonalFinance.Application.Tests.Unit.Auth;

/// <summary>
/// ResendEmailVerificationUseCase (#404): reenvia o código de verificação. Anti-enumeração: nunca lança nem
/// enfileira para e-mail inexistente, inativo, removido ou já confirmado.
/// </summary>
public class ResendEmailVerificationUseCaseTests
{
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly IssuerHarness _h;

    public ResendEmailVerificationUseCaseTests() => _h = new IssuerHarness(_uow);

    private ResendEmailVerificationUseCase Sut(IValidator<EmailRequestDto>? validator = null) =>
        UseCaseFactory.Create<ResendEmailVerificationUseCase>(
            _userRepo.Object, _h.Issuer, _uow.Object,
            validator ?? TestValidators.Valid<EmailRequestDto>());

    private User ExistingUser()
    {
        var user = User.Create("Ana", "ana@x.com", "hash");
        _userRepo.Setup(r => r.GetByEmailAsync("ana@x.com", It.IsAny<CancellationToken>())).ReturnsAsync(user);
        return user;
    }

    [Fact(DisplayName = "Usuário ativo não verificado: emite token de verificação e enfileira o e-mail com link /confirm-email")]
    public async Task Execute_UnverifiedUser_ShouldIssueVerification()
    {
        var user = ExistingUser();

        await Sut().ExecuteAsync(new EmailRequestDto("ana@x.com"));

        _h.Added.Should().ContainSingle().Which.Purpose.Should().Be(UserTokenPurpose.EmailVerification);
        _h.Added[0].UserId.Should().Be(user.Id);
        var message = _h.Enqueued.Should().ContainSingle().Subject;
        message.To.Should().Be("ana@x.com");
        message.HtmlBody.Should().Contain(IssuerHarness.Code).And.Contain("/confirm-email#email=ana%40x.com");
    }

    [Fact(DisplayName = "E-mail é normalizado (trim + lowercase) antes da busca")]
    public async Task Execute_ShouldNormalizeEmail()
    {
        ExistingUser();

        await Sut().ExecuteAsync(new EmailRequestDto("  ANA@X.com "));

        _userRepo.Verify(r => r.GetByEmailAsync("ana@x.com", It.IsAny<CancellationToken>()), Times.Once);
        _h.Enqueued.Should().ContainSingle();
    }

    [Fact(DisplayName = "E-mail já confirmado: não lança e não enfileira")]
    public async Task Execute_AlreadyConfirmed_ShouldDoNothing()
    {
        var user = ExistingUser();
        user.ConfirmEmail(DateTime.UtcNow);

        var act = () => Sut().ExecuteAsync(new EmailRequestDto("ana@x.com"));

        await act.Should().NotThrowAsync();
        _h.Enqueued.Should().BeEmpty();
        _h.Added.Should().BeEmpty();
    }

    [Fact(DisplayName = "E-mail inexistente: não lança, não enfileira, não commita")]
    public async Task Execute_UnknownEmail_ShouldDoNothingSilently()
    {
        _userRepo.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        var act = () => Sut().ExecuteAsync(new EmailRequestDto("ghost@x.com"));

        await act.Should().NotThrowAsync();
        _h.Enqueued.Should().BeEmpty();
        _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "Usuário inativo ou removido: nenhum token nem e-mail")]
    public async Task Execute_InactiveOrDeletedUser_ShouldDoNothing()
    {
        var user = ExistingUser();
        user.Deactivate();

        var act = () => Sut().ExecuteAsync(new EmailRequestDto("ana@x.com"));
        await act.Should().NotThrowAsync();

        user.Reactivate();
        user.SoftDelete();
        await act.Should().NotThrowAsync();

        _h.Enqueued.Should().BeEmpty();
        _h.Added.Should().BeEmpty();
    }

    [Fact(DisplayName = "Dentro do cooldown: não lança e não enfileira de novo")]
    public async Task Execute_WithinCooldown_ShouldNotEnqueueAgain()
    {
        var user = ExistingUser();
        _h.SetupLatestToken(user, UserTokenPurpose.EmailVerification, TimeSpan.FromSeconds(5));

        var act = () => Sut().ExecuteAsync(new EmailRequestDto("ana@x.com"));

        await act.Should().NotThrowAsync();
        _h.Enqueued.Should().BeEmpty();
    }

    [Fact(DisplayName = "Validator reprovado: ValidationException sem tocar repositório nem fila")]
    public async Task Execute_WhenValidatorFails_ShouldThrowAndTouchNothing()
    {
        var sut = Sut(TestValidators.Invalid<EmailRequestDto>());

        await Assert.ThrowsAsync<ValidationException>(() => sut.ExecuteAsync(new EmailRequestDto("ana@x.com")));

        _userRepo.Invocations.Should().BeEmpty();
        _h.Enqueued.Should().BeEmpty();
    }
}
