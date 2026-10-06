using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using PersonalFinance.Application.DTOs.Email;
using PersonalFinance.Application.Interfaces;

namespace PersonalFinance.Infrastructure.Services;

/// <summary>
/// Envia e-mails pela API HTTP do Brevo. Resposta não-2xx é logada sem lançar;
/// nunca loga corpo, código nem ApiKey.
/// </summary>
public sealed class BrevoEmailSender : IEmailSender
{
    private const string Endpoint = "https://api.brevo.com/v3/smtp/email";

    private readonly HttpClient _http;
    private readonly BrevoOptions _options;
    private readonly ILogger<BrevoEmailSender> _logger;

    public BrevoEmailSender(HttpClient http, BrevoOptions options, ILogger<BrevoEmailSender> logger)
    {
        _http = http;
        _options = options;
        _logger = logger;
    }

    public async Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        var payload = new
        {
            sender = new { email = _options.SenderEmail, name = _options.SenderName },
            to = new[] { new { email = message.To } },
            subject = message.Subject,
            htmlContent = message.HtmlBody
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint)
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Add("api-key", _options.ApiKey);

        try
        {
            using var response = await _http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                // Só o status: o corpo da resposta/e-mail pode conter dados sensíveis
                _logger.LogWarning("Brevo rejected the email request with status {StatusCode}.", (int)response.StatusCode);
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // Timeout do HttpClient (token do chamador intacto): falha logada, sem derrubar o consumidor da fila.
            // Cancelamento do host (ct cancelado) segue propagando.
            _logger.LogWarning("Brevo email request timed out.");
        }
    }
}
