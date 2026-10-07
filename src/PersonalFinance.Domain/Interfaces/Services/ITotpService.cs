namespace PersonalFinance.Domain.Interfaces.Services;

/// <summary>TOTP (RFC 6238): geração de secret, URI otpauth e validação de código.</summary>
public interface ITotpService
{
    /// <summary>Gera um secret aleatório em Base32.</summary>
    string GenerateSecret();

    /// <summary>Monta a URI otpauth:// (issuer MonkeyBomb, label = e-mail) para o app autenticador.</summary>
    string BuildOtpAuthUri(string secret, string email);

    /// <summary>Valida o código na janela ±1 step. Retorna o time step do código aceito ou null se inválido.</summary>
    long? ValidateCode(string secret, string code, DateTime utcNow);
}
