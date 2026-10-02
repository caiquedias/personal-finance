namespace PersonalFinance.Api.Options;

/// <summary>
/// Parâmetros do rate limit do login (seção RateLimiting:Login).
/// </summary>
public sealed class LoginRateLimitOptions
{
    /// <summary>Requisições permitidas por janela, por IP.</summary>
    public int PermitLimit { get; set; } = 10;

    /// <summary>Duração da janela em segundos.</summary>
    public int WindowSeconds { get; set; } = 60;
}
