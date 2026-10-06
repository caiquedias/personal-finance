using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using PersonalFinance.Api.BackgroundServices;
using PersonalFinance.Api.Tests.Unit.Support;
using PersonalFinance.Application.DTOs.Email;
using PersonalFinance.Application.Interfaces;
using PersonalFinance.Application.Options;
using PersonalFinance.Infrastructure.Services;
using Xunit;

namespace PersonalFinance.Api.Tests.Unit;

/// <summary>
/// EmailDispatchHostedService (#404): drena a fila e envia via IEmailSender. Exceção do sender é logada e NÃO
/// derruba o loop. Com Email:Enabled=false só loga destinatário/assunto — nunca o corpo (que tem o código).
/// </summary>
public class EmailDispatchHostedServiceTests
{
    private const string Secret = "729104";

    private readonly Mock<IEmailSender> _sender = new();
    private readonly ListLogger<EmailDispatchHostedService> _logger = new();
    private readonly ChannelEmailQueue _queue = new();
    private readonly List<string> _sentSubjects = new();

    private EmailDispatchHostedService Build(bool enabled = true)
    {
        _sender.Setup(s => s.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
               .Callback<EmailMessage, CancellationToken>((m, _) => { lock (_sentSubjects) _sentSubjects.Add(m.Subject); })
               .Returns(Task.CompletedTask);

        var services = new ServiceCollection();
        services.AddSingleton(_sender.Object);
        var provider = services.BuildServiceProvider();

        return new EmailDispatchHostedService(
            _queue, provider.GetRequiredService<IServiceScopeFactory>(),
            new EmailOptions { Enabled = enabled }, _logger);
    }

    private static EmailMessage Msg(string subject) => new("ana@x.com", subject, $"<p>Código {Secret}</p>");

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition() && DateTime.UtcNow < deadline)
            await Task.Delay(20);
    }

    private int SentCount() { lock (_sentSubjects) return _sentSubjects.Count; }

    [Fact(DisplayName = "Enabled=true deve enviar a mensagem enfileirada via IEmailSender")]
    public async Task Enabled_ShouldSendQueuedMessage()
    {
        var sut = Build();
        await sut.StartAsync(CancellationToken.None);

        _queue.TryEnqueue(Msg("um"));
        await WaitUntilAsync(() => SentCount() >= 1);
        await sut.StopAsync(CancellationToken.None);

        _sender.Verify(s => s.SendAsync(
            It.Is<EmailMessage>(m => m.To == "ana@x.com" && m.Subject == "um"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "Deve enviar várias mensagens em ordem")]
    public async Task Enabled_ShouldSendInOrder()
    {
        var sut = Build();
        await sut.StartAsync(CancellationToken.None);

        _queue.TryEnqueue(Msg("1"));
        _queue.TryEnqueue(Msg("2"));
        _queue.TryEnqueue(Msg("3"));
        await WaitUntilAsync(() => SentCount() >= 3);
        await sut.StopAsync(CancellationToken.None);

        lock (_sentSubjects) _sentSubjects.Should().Equal("1", "2", "3");
    }

    [Fact(DisplayName = "Exceção do sender é logada e o loop segue enviando as próximas mensagens")]
    public async Task SenderThrows_ShouldLogAndKeepProcessing()
    {
        var sut = Build();
        _sender.Setup(s => s.SendAsync(It.Is<EmailMessage>(m => m.Subject == "falha"), It.IsAny<CancellationToken>()))
               .ThrowsAsync(new InvalidOperationException("provedor fora do ar"));
        await sut.StartAsync(CancellationToken.None);

        _queue.TryEnqueue(Msg("falha"));
        _queue.TryEnqueue(Msg("depois"));
        await WaitUntilAsync(() => SentCount() >= 1);

        _logger.Entries.Should().Contain(e => e.Level >= LogLevel.Warning);
        lock (_sentSubjects) _sentSubjects.Should().Contain("depois");
        sut.ExecuteTask.Should().NotBeNull();
        sut.ExecuteTask!.IsCompleted.Should().BeFalse("o loop continua aguardando novas mensagens");

        var stop = () => sut.StopAsync(CancellationToken.None);
        await stop.Should().NotThrowAsync();
    }

    [Fact(DisplayName = "Log de falha do sender não contém o corpo do e-mail (código)")]
    public async Task SenderThrows_ShouldNotLogBody()
    {
        var sut = Build();
        _sender.Setup(s => s.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
               .ThrowsAsync(new InvalidOperationException("provedor fora do ar"));
        await sut.StartAsync(CancellationToken.None);

        _queue.TryEnqueue(Msg("falha"));
        await WaitUntilAsync(() => _logger.Entries.Any(e => e.Level >= LogLevel.Warning));
        await sut.StopAsync(CancellationToken.None);

        _logger.AllText.Should().NotContain(Secret);
    }

    [Fact(DisplayName = "Enabled=false: não chama o sender; loga destinatário e assunto, nunca o corpo/código")]
    public async Task Disabled_ShouldOnlyLogRecipientAndSubject()
    {
        var sut = Build(enabled: false);
        await sut.StartAsync(CancellationToken.None);

        _queue.TryEnqueue(Msg("Assunto visível"));
        await WaitUntilAsync(() => _logger.Entries.Count > 0);
        await sut.StopAsync(CancellationToken.None);

        _sender.Verify(s => s.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
        _logger.AllText.Should().Contain("ana@x.com").And.Contain("Assunto visível");
        _logger.AllText.Should().NotContain(Secret);
    }

    [Fact(DisplayName = "StopAsync deve encerrar o loop mesmo com a fila vazia")]
    public async Task Stop_ShouldCompleteExecuteTask()
    {
        var sut = Build();
        await sut.StartAsync(CancellationToken.None);

        await sut.StopAsync(CancellationToken.None);

        sut.ExecuteTask!.IsCompleted.Should().BeTrue();
    }
}
