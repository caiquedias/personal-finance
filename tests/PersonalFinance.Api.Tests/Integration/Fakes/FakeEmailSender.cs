using Microsoft.Extensions.DependencyInjection;
using PersonalFinance.Application.DTOs.Email;
using PersonalFinance.Application.Interfaces;
using System.Net;
using System.Text.RegularExpressions;

namespace PersonalFinance.Api.Tests.Integration.Fakes;

/// <summary>
/// Substitui o IEmailSender (Brevo) nos testes de integração (#404): guarda as mensagens enviadas pelo
/// EmailDispatchHostedService e permite aguardar/extrair o código de 6 dígitos do corpo.
/// </summary>
public sealed class FakeEmailSender : IEmailSender
{
    private readonly object _lock = new();
    private readonly List<EmailMessage> _sent = new();

    public IReadOnlyList<EmailMessage> Sent
    {
        get { lock (_lock) return _sent.ToList(); }
    }

    public Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        lock (_lock) _sent.Add(message);
        return Task.CompletedTask;
    }

    /// <summary>E-mails enviados para o destinatário cujo corpo contém o trecho (ex.: "/reset-password").</summary>
    public IReadOnlyList<EmailMessage> SentTo(string to, string bodyContains) =>
        Sent.Where(m => string.Equals(m.To, to, StringComparison.OrdinalIgnoreCase)
                        && m.HtmlBody.Contains(bodyContains, StringComparison.Ordinal)).ToList();

    /// <summary>
    /// Barreira determinística: enfileira uma mensagem-sentinela e espera o envio dela. Como a fila é FIFO,
    /// tudo que foi enfileirado antes já foi processado — permite asserir "nenhum e-mail foi enviado".
    /// </summary>
    public async Task DrainAsync(IServiceProvider services)
    {
        var queue = services.GetRequiredService<IEmailQueue>();
        var marker = $"sentinel-{Guid.NewGuid():N}";
        queue.TryEnqueue(new EmailMessage("sentinel@drain.test", marker, "<p>sentinel</p>"));
        await WaitUntilAsync(() => Sent.Any(m => m.Subject == marker), TimeSpan.FromSeconds(10), "sentinela do dreno");
    }

    /// <summary>Aguarda o e-mail para o destinatário com o trecho no corpo e devolve o código de 6 dígitos.</summary>
    public async Task<string> WaitForCodeAsync(string to, string bodyContains, int minCount = 1)
    {
        await WaitUntilAsync(() => SentTo(to, bodyContains).Count >= minCount, TimeSpan.FromSeconds(10),
            $"e-mail para {to} ({bodyContains})");
        return ExtractCode(SentTo(to, bodyContains).Last(), to);
    }

    /// <summary>
    /// Extrai o código de 6 dígitos do corpo, ignorando links (href) e o próprio e-mail do destinatário
    /// (que pode conter sequências numéricas).
    /// </summary>
    public static string ExtractCode(EmailMessage message, string to)
    {
        var body = Regex.Replace(message.HtmlBody, "href=\"[^\"]*\"", string.Empty, RegexOptions.IgnoreCase);
        body = body.Replace(WebUtility.HtmlEncode(to), string.Empty).Replace(to, string.Empty);
        var match = Regex.Match(body, @"(?<!\d)\d{6}(?!\d)");
        if (!match.Success)
            throw new InvalidOperationException("Código de 6 dígitos não encontrado no corpo do e-mail.");
        return match.Value;
    }

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout, string what)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException($"Tempo esgotado aguardando {what}.");
            await Task.Delay(25);
        }
    }
}
