namespace PersonalFinance.Application.Options;

/// <summary>Parâmetros dos códigos de uso único (seção Auth:UserTokens).</summary>
public sealed class UserTokenOptions
{
    /// <summary>Chave HMAC (Base64 de 32 bytes). Segredo — nunca logar.</summary>
    public string? HmacKey { get; set; }

    public int CodeTtlMinutes { get; set; } = 10;

    public int MaxAttempts { get; set; } = 3;

    public int ResendCooldownSeconds { get; set; } = 60;
}
