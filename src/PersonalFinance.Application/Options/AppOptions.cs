namespace PersonalFinance.Application.Options;

/// <summary>Parâmetros da aplicação (seção App).</summary>
public sealed class AppOptions
{
    /// <summary>URL base do frontend usada nos links dos e-mails.</summary>
    public string FrontendBaseUrl { get; set; } = string.Empty;
}
