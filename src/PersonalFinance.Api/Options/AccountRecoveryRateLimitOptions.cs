namespace PersonalFinance.Api.Options;

/// <summary>
/// Parâmetros do rate limit dos endpoints anônimos de recuperação de conta (seção RateLimiting:AccountRecovery).
/// </summary>
public sealed class AccountRecoveryRateLimitOptions
{
    /// <summary>Requisições permitidas por janela, por IP.</summary>
    public int PermitLimit { get; set; } = 5;

    /// <summary>Duração da janela em segundos.</summary>
    public int WindowSeconds { get; set; } = 60;
}
