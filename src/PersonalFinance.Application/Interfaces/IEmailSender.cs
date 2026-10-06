using PersonalFinance.Application.DTOs.Email;

namespace PersonalFinance.Application.Interfaces;

/// <summary>Envio de e-mail por um provedor externo.</summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken ct);
}
