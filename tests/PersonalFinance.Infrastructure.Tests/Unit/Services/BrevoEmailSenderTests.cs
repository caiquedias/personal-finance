using FluentAssertions;
using Microsoft.Extensions.Logging;
using PersonalFinance.Application.DTOs.Email;
using PersonalFinance.Infrastructure.Services;
using PersonalFinance.Infrastructure.Tests.Unit.Support;
using System.Net;
using System.Text.Json;
using Xunit;

namespace PersonalFinance.Infrastructure.Tests.Unit.Services;

/// <summary>
/// BrevoEmailSender (#404): POST https://api.brevo.com/v3/smtp/email com header api-key. Resposta não-2xx é
/// logada SEM lançar (e sem vazar chave/corpo do e-mail). O HttpClient é injetado (typed client).
/// </summary>
public class BrevoEmailSenderTests
{
    private const string ApiKey = "xkeysib-SEGREDO-DE-TESTE";
    private const string Secret = "483920";

    private readonly ListLogger<BrevoEmailSender> _logger = new();

    private static BrevoOptions Options() => new()
    {
        ApiKey = ApiKey,
        SenderEmail = "no-reply@monkeybomb.test",
        SenderName = "MonkeyBomb"
    };

    private BrevoEmailSender Build(StubHttpMessageHandler handler) =>
        new(new HttpClient(handler), Options(), _logger);

    private static EmailMessage Message() =>
        new("ana@x.com", "Seu código", $"<p>Código: {Secret}</p>");

    [Fact(DisplayName = "Deve fazer POST em https://api.brevo.com/v3/smtp/email")]
    public async Task Send_ShouldPostToBrevoEndpoint()
    {
        var handler = new StubHttpMessageHandler();

        await Build(handler).SendAsync(Message(), CancellationToken.None);

        handler.CallCount.Should().Be(1);
        handler.Method.Should().Be(HttpMethod.Post);
        handler.RequestUri!.ToString().Should().Be("https://api.brevo.com/v3/smtp/email");
    }

    [Fact(DisplayName = "Deve enviar a chave no header api-key (nunca na URL)")]
    public async Task Send_ShouldSendApiKeyHeader()
    {
        var handler = new StubHttpMessageHandler();

        await Build(handler).SendAsync(Message(), CancellationToken.None);

        handler.Headers.Should().ContainKey("api-key").WhoseValue.Should().Be(ApiKey);
        handler.RequestUri!.ToString().Should().NotContain(ApiKey);
    }

    [Fact(DisplayName = "Corpo JSON deve ter sender, to, subject e htmlContent")]
    public async Task Send_ShouldSerializeBrevoPayload()
    {
        var handler = new StubHttpMessageHandler();

        await Build(handler).SendAsync(Message(), CancellationToken.None);

        using var doc = JsonDocument.Parse(handler.RequestBody!);
        var root = doc.RootElement;
        root.GetProperty("sender").GetProperty("email").GetString().Should().Be("no-reply@monkeybomb.test");
        root.GetProperty("sender").GetProperty("name").GetString().Should().Be("MonkeyBomb");
        root.GetProperty("to").EnumerateArray().Single().GetProperty("email").GetString().Should().Be("ana@x.com");
        root.GetProperty("subject").GetString().Should().Be("Seu código");
        root.GetProperty("htmlContent").GetString().Should().Contain(Secret);
    }

    [Theory(DisplayName = "Resposta não-2xx deve ser logada sem lançar exceção")]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task Send_NonSuccess_ShouldLogWithoutThrowing(HttpStatusCode status)
    {
        var handler = new StubHttpMessageHandler(status, "{\"message\":\"erro do provedor\"}");

        var act = () => Build(handler).SendAsync(Message(), CancellationToken.None);

        await act.Should().NotThrowAsync();
        _logger.Entries.Should().Contain(e => e.Level >= LogLevel.Warning);
        _logger.AllText.Should().Contain(((int)status).ToString());
    }

    [Fact(DisplayName = "Log de falha não vaza a api-key nem o código do e-mail")]
    public async Task Send_NonSuccess_ShouldNotLeakSecrets()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.Unauthorized, "{\"message\":\"invalid key\"}");

        await Build(handler).SendAsync(Message(), CancellationToken.None);

        _logger.AllText.Should().NotContain(ApiKey).And.NotContain(Secret);
    }

    // ── C1: timeout do HttpClient ─────────────────────────────────────────────

    private BrevoEmailSender BuildWithTimeout(HttpMessageHandler handler, TimeSpan timeout) =>
        new(new HttpClient(handler) { Timeout = timeout }, Options(), _logger);

    [Fact(DisplayName = "Timeout do HttpClient: loga aviso sem lançar (não pode derrubar o consumidor da fila)")]
    public async Task Send_OnHttpClientTimeout_ShouldLogWithoutThrowing()
    {
        var sut = BuildWithTimeout(new HangingHttpMessageHandler(), TimeSpan.FromMilliseconds(50));

        var act = () => sut.SendAsync(Message(), CancellationToken.None);

        await act.Should().NotThrowAsync();
        _logger.Entries.Should().Contain(e => e.Level >= LogLevel.Warning);
    }

    [Fact(DisplayName = "Log de timeout não vaza api-key nem o código do e-mail")]
    public async Task Send_OnHttpClientTimeout_ShouldNotLeakSecrets()
    {
        var sut = BuildWithTimeout(new HangingHttpMessageHandler(), TimeSpan.FromMilliseconds(50));

        await sut.SendAsync(Message(), CancellationToken.None);

        _logger.AllText.Should().NotContain(ApiKey).And.NotContain(Secret);
    }

    [Fact(DisplayName = "Cancelamento do host (token cancelado) continua propagando OperationCanceledException")]
    public async Task Send_OnHostCancellation_ShouldPropagate()
    {
        var sut = BuildWithTimeout(new HangingHttpMessageHandler(), TimeSpan.FromSeconds(30));
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        var act = () => sut.SendAsync(Message(), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact(DisplayName = "Resposta 2xx não gera log de aviso/erro")]
    public async Task Send_Success_ShouldNotLogWarnings()
    {
        await Build(new StubHttpMessageHandler(HttpStatusCode.Created)).SendAsync(Message(), CancellationToken.None);

        _logger.Entries.Should().NotContain(e => e.Level >= LogLevel.Warning);
    }
}
