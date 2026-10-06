using PersonalFinance.Application.DTOs.Email;

namespace PersonalFinance.Application.Interfaces;

/// <summary>Fila em memória de e-mails a enviar.</summary>
public interface IEmailQueue
{
    bool TryEnqueue(EmailMessage message);

    ValueTask<EmailMessage> DequeueAsync(CancellationToken ct);
}
