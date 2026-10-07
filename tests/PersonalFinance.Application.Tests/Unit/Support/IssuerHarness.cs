using Moq;
using PersonalFinance.Application.DTOs.Email;
using PersonalFinance.Application.Interfaces;
using PersonalFinance.Application.Options;
using PersonalFinance.Application.Services.Auth;
using PersonalFinance.Domain.Entities.Auth;
using PersonalFinance.Domain.Enums;
using PersonalFinance.Domain.Interfaces.Repositories;

namespace PersonalFinance.Application.Tests.Unit.Support;

/// <summary>
/// Monta um UserTokenIssuer REAL sobre dependências mockadas (repositório de tokens, serviço de código,
/// fila de e-mail) para os testes dos use cases que emitem código (#404). Código gerado fixo "123456".
/// Não configura o IUnitOfWork (o teste dono decide — ex.: AuditTrace).
/// </summary>
public sealed class IssuerHarness
{
    public const string Code = "123456";
    public const string ComputedHash = "computed-hash";

    public Mock<IUserTokenRepository> Tokens { get; } = new();
    public Mock<IOneTimeCodeService> Codes { get; } = new();
    public Mock<IEmailQueue> Queue { get; } = new();

    /// <summary>E-mails enfileirados, na ordem.</summary>
    public List<EmailMessage> Enqueued { get; } = new();

    /// <summary>Tokens gravados via AddAsync, na ordem.</summary>
    public List<UserToken> Added { get; } = new();

    /// <summary>Ordem relativa de remove/add/enqueue.</summary>
    public List<string> Order { get; } = new();

    public UserTokenOptions Options { get; } = new()
    {
        HmacKey = "AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8=",
        CodeTtlMinutes = 10,
        MaxAttempts = 3,
        ResendCooldownSeconds = 60
    };

    public AppOptions App { get; } = new() { FrontendBaseUrl = "https://app.example.com" };

    public UserTokenIssuer Issuer { get; }

    public IssuerHarness(Mock<IUnitOfWork> uow)
    {
        Codes.Setup(c => c.GenerateCode()).Returns(Code);
        Codes.Setup(c => c.ComputeHash(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<UserTokenPurpose>(), It.IsAny<string>()))
             .Returns(ComputedHash);

        Tokens.Setup(t => t.RemoveAllAsync(It.IsAny<Guid>(), It.IsAny<UserTokenPurpose>(), It.IsAny<CancellationToken>()))
              .Callback(() => Order.Add("remove"))
              .Returns(Task.CompletedTask);
        Tokens.Setup(t => t.AddAsync(It.IsAny<UserToken>(), It.IsAny<CancellationToken>()))
              .Callback<UserToken, CancellationToken>((token, _) => { Added.Add(token); Order.Add("add"); })
              .Returns(Task.CompletedTask);

        Queue.Setup(q => q.TryEnqueue(It.IsAny<EmailMessage>()))
             .Callback<EmailMessage>(m => { Enqueued.Add(m); Order.Add("enqueue"); })
             .Returns(true);

        Issuer = UseCaseFactory.Create<UserTokenIssuer>(
            Tokens.Object, Codes.Object, Queue.Object, uow.Object, Options, new AuthEmailComposer(App));
    }

    /// <summary>Último token de (usuário, propósito) emitido há <paramref name="age"/> — simula cooldown.</summary>
    public void SetupLatestToken(User user, UserTokenPurpose purpose, TimeSpan age)
    {
        var token = UserToken.Create(user.Id, purpose, DateTime.UtcNow - age, TimeSpan.FromMinutes(10));
        token.SetTokenHash("existing-hash");
        Tokens.Setup(t => t.GetLatestAsync(user.Id, purpose, It.IsAny<CancellationToken>())).ReturnsAsync(token);
    }
}
