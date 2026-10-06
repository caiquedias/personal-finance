using FluentAssertions;
using Moq;
using PersonalFinance.Application.Tests.Unit.Support;
using PersonalFinance.Domain.Entities.Auth;
using PersonalFinance.Domain.Enums;
using PersonalFinance.Domain.Interfaces.Repositories;
using Xunit;

namespace PersonalFinance.Application.Tests.Unit.Auth;

/// <summary>
/// UserTokenIssuer (#404): cooldown por conta, hard delete dos anteriores, criação do token com HMAC,
/// commit e SÓ DEPOIS o enfileiramento do e-mail (nunca enviar código que não foi persistido).
/// </summary>
public class UserTokenIssuerTests
{
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly IssuerHarness _h;

    public UserTokenIssuerTests()
    {
        _h = new IssuerHarness(_uow);
        _uow.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>()))
            .Callback(() => _h.Order.Add("commit"))
            .Returns(Task.CompletedTask);
    }

    private static User ActiveUser() => User.Create("Ana", "ana@x.com", "hash");

    [Fact(DisplayName = "Deve remover os anteriores, gravar o token, commitar e só então enfileirar o e-mail")]
    public async Task Issue_ShouldRemoveAddCommitThenEnqueue()
    {
        var user = ActiveUser();

        var issued = await _h.Issuer.IssueAsync(user, UserTokenPurpose.PasswordReset);

        issued.Should().BeTrue();
        _h.Order.Should().Equal("remove", "add", "commit", "enqueue");
        _h.Tokens.Verify(t => t.RemoveAllAsync(user.Id, UserTokenPurpose.PasswordReset, It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "Token gravado pertence ao usuário/propósito, expira em CodeTtlMinutes e guarda só o hash")]
    public async Task Issue_ShouldPersistTokenWithHashOnly()
    {
        var user = ActiveUser();
        _h.Options.CodeTtlMinutes = 7;

        await _h.Issuer.IssueAsync(user, UserTokenPurpose.EmailVerification);

        var token = _h.Added.Should().ContainSingle().Subject;
        token.UserId.Should().Be(user.Id);
        token.Purpose.Should().Be(UserTokenPurpose.EmailVerification);
        token.TokenHash.Should().Be(IssuerHarness.ComputedHash);
        token.TokenHash.Should().NotContain(IssuerHarness.Code);
        (token.ExpiresAt - token.CreatedAt).Should().Be(TimeSpan.FromMinutes(7));
        token.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(30));
    }

    [Fact(DisplayName = "O hash deve ser calculado com Id do token, Id do usuário, propósito e o código gerado")]
    public async Task Issue_ShouldComputeHashOverTokenIdUserIdPurposeAndCode()
    {
        var user = ActiveUser();

        await _h.Issuer.IssueAsync(user, UserTokenPurpose.PasswordReset);

        var token = _h.Added.Single();
        _h.Codes.Verify(c => c.ComputeHash(token.Id, user.Id, UserTokenPurpose.PasswordReset, IssuerHarness.Code), Times.Once);
    }

    [Fact(DisplayName = "E-mail enfileirado vai para o usuário, contém o código e o link do propósito")]
    public async Task Issue_ShouldEnqueueEmailWithCode()
    {
        var user = ActiveUser();

        await _h.Issuer.IssueAsync(user, UserTokenPurpose.PasswordReset);

        var message = _h.Enqueued.Should().ContainSingle().Subject;
        message.To.Should().Be("ana@x.com");
        message.HtmlBody.Should().Contain(IssuerHarness.Code);
        message.HtmlBody.Should().Contain("/reset-password#email=ana%40x.com");
    }

    [Fact(DisplayName = "Propósito EmailVerification usa o link /confirm-email")]
    public async Task Issue_EmailVerification_ShouldUseConfirmLink()
    {
        await _h.Issuer.IssueAsync(ActiveUser(), UserTokenPurpose.EmailVerification);

        _h.Enqueued.Single().HtmlBody.Should().Contain("/confirm-email#email=ana%40x.com");
    }

    [Fact(DisplayName = "Dentro do cooldown: não remove, não grava, não commita e não enfileira (retorna false)")]
    public async Task Issue_WithinCooldown_ShouldDoNothing()
    {
        var user = ActiveUser();
        _h.SetupLatestToken(user, UserTokenPurpose.PasswordReset, age: TimeSpan.FromSeconds(10));

        var issued = await _h.Issuer.IssueAsync(user, UserTokenPurpose.PasswordReset);

        issued.Should().BeFalse();
        _h.Order.Should().BeEmpty();
        _h.Enqueued.Should().BeEmpty();
        _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "Cooldown vencido (token de 2 min, cooldown de 60s) permite nova emissão")]
    public async Task Issue_AfterCooldown_ShouldIssueAgain()
    {
        var user = ActiveUser();
        _h.SetupLatestToken(user, UserTokenPurpose.PasswordReset, age: TimeSpan.FromMinutes(2));

        var issued = await _h.Issuer.IssueAsync(user, UserTokenPurpose.PasswordReset);

        issued.Should().BeTrue();
        _h.Enqueued.Should().ContainSingle();
    }

    [Fact(DisplayName = "Cooldown respeita ResendCooldownSeconds configurado")]
    public async Task Issue_CooldownFollowsOptions()
    {
        var user = ActiveUser();
        _h.Options.ResendCooldownSeconds = 5;
        _h.SetupLatestToken(user, UserTokenPurpose.PasswordReset, age: TimeSpan.FromSeconds(30));

        (await _h.Issuer.IssueAsync(user, UserTokenPurpose.PasswordReset)).Should().BeTrue();
    }

    [Fact(DisplayName = "Cooldown é por propósito: token recente de verificação não bloqueia reset")]
    public async Task Issue_CooldownIsPerPurpose()
    {
        var user = ActiveUser();
        _h.SetupLatestToken(user, UserTokenPurpose.EmailVerification, age: TimeSpan.FromSeconds(1));

        var issued = await _h.Issuer.IssueAsync(user, UserTokenPurpose.PasswordReset);

        issued.Should().BeTrue();
    }

    [Fact(DisplayName = "Falha no commit não pode enfileirar o e-mail")]
    public async Task Issue_WhenCommitFails_ShouldNotEnqueue()
    {
        _uow.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("db"));

        var act = () => _h.Issuer.IssueAsync(ActiveUser(), UserTokenPurpose.PasswordReset);

        await act.Should().ThrowAsync<InvalidOperationException>();
        _h.Enqueued.Should().BeEmpty();
    }

    [Fact(DisplayName = "Fila cheia (TryEnqueue=false) não deve lançar nem desfazer a emissão")]
    public async Task Issue_WhenQueueIsFull_ShouldNotThrow()
    {
        _h.Queue.Setup(q => q.TryEnqueue(It.IsAny<PersonalFinance.Application.DTOs.Email.EmailMessage>())).Returns(false);

        var act = () => _h.Issuer.IssueAsync(ActiveUser(), UserTokenPurpose.PasswordReset);

        await act.Should().NotThrowAsync();
        _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "Cada emissão gera um código novo (GenerateCode chamado uma vez por emissão)")]
    public async Task Issue_ShouldGenerateCodeOncePerIssue()
    {
        await _h.Issuer.IssueAsync(ActiveUser(), UserTokenPurpose.PasswordReset);

        _h.Codes.Verify(c => c.GenerateCode(), Times.Once);
    }
}
