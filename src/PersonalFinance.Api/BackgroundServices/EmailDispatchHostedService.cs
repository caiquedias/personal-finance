using PersonalFinance.Application.Interfaces;
using PersonalFinance.Application.Options;

namespace PersonalFinance.Api.BackgroundServices;

/// <summary>
/// Drena a fila de e-mails e envia via IEmailSender. Falha de envio é logada e não derruba o loop.
/// Com Email:Enabled=false só loga destinatário e assunto (nunca o corpo, que contém o código).
/// </summary>
public sealed class EmailDispatchHostedService : BackgroundService
{
    private readonly IEmailQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly EmailOptions _options;
    private readonly ILogger<EmailDispatchHostedService> _logger;

    public EmailDispatchHostedService(
        IEmailQueue queue,
        IServiceScopeFactory scopeFactory,
        EmailOptions options,
        ILogger<EmailDispatchHostedService> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var message = await _queue.DequeueAsync(stoppingToken);

                if (!_options.Enabled)
                {
                    _logger.LogInformation(
                        "Email sending disabled; skipped message to {Recipient} with subject {Subject}.",
                        message.To, message.Subject);
                    continue;
                }

                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var sender = scope.ServiceProvider.GetRequiredService<IEmailSender>();
                    await sender.SendAsync(message, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    // Sem corpo/código no log; só o tipo da exceção
                    _logger.LogWarning(
                        "Failed to send email to {Recipient}: {ExceptionType}.",
                        message.To, ex.GetType().Name);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Encerramento normal da aplicação
        }
    }
}
