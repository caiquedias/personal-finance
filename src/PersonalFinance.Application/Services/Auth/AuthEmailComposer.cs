using System.Net;
using PersonalFinance.Application.DTOs.Email;
using PersonalFinance.Application.Options;

namespace PersonalFinance.Application.Services.Auth;

/// <summary>
/// Monta os e-mails de reset de senha e verificação de e-mail.
/// O link leva só o e-mail (no fragmento); o código vai exclusivamente no corpo, nunca na URL.
/// </summary>
public sealed class AuthEmailComposer
{
    private readonly string _baseUrl;

    public AuthEmailComposer(AppOptions options)
    {
        _baseUrl = (options.FrontendBaseUrl ?? string.Empty).TrimEnd('/');
    }

    public EmailMessage ComposePasswordReset(string email, string code, int ttlMinutes) =>
        Compose(
            email, code, ttlMinutes,
            subject: "Redefinição de senha - Personal Finance",
            intro: "Recebemos um pedido para redefinir a senha da sua conta.",
            path: "reset-password",
            linkText: "Redefinir senha");

    public EmailMessage ComposeEmailVerification(string email, string code, int ttlMinutes) =>
        Compose(
            email, code, ttlMinutes,
            subject: "Confirme seu e-mail - Personal Finance",
            intro: "Use o código abaixo para confirmar o seu e-mail.",
            path: "confirm-email",
            linkText: "Confirmar e-mail");

    private EmailMessage Compose(
        string email, string code, int ttlMinutes, string subject, string intro, string path, string linkText)
    {
        // Escape de URL no fragmento; o href nunca contém o código
        var link = $"{_baseUrl}/{path}#email={Uri.EscapeDataString(email)}";
        var body =
            $"<p>{intro}</p>" +
            $"<p>Conta: {WebUtility.HtmlEncode(email)}</p>" +
            $"<p>Seu código: <strong>{WebUtility.HtmlEncode(code)}</strong></p>" +
            $"<p>O código expira em {ttlMinutes} minutos.</p>" +
            $"<p><a href=\"{WebUtility.HtmlEncode(link)}\">{linkText}</a></p>" +
            "<p>Se você não solicitou, ignore este e-mail.</p>";

        return new EmailMessage(email, subject, body);
    }
}
