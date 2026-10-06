namespace PersonalFinance.Infrastructure.Services;

/// <summary>Credenciais e remetente do Brevo (seção Email:Brevo).</summary>
public sealed class BrevoOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string SenderEmail { get; set; } = string.Empty;
    public string SenderName { get; set; } = string.Empty;
}
