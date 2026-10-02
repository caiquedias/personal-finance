using OtpNet;
using PersonalFinance.Domain.Interfaces.Services;

namespace PersonalFinance.Infrastructure.Auth;

/// <summary>
/// TOTP RFC 6238 (SHA-1, 6 dígitos, passo de 30s) sobre Otp.NET.
/// Janela de tolerância de ±1 step. Entrada malformada nunca lança — retorna null.
/// </summary>
public sealed class TotpService : ITotpService
{
    private const string Issuer = "MonkeyBomb";
    private const int SecretBytes = 20;   // 160 bits
    private const int CodeDigits = 6;
    private const int StepSeconds = 30;

    public string GenerateSecret()
        => Base32Encoding.ToString(KeyGeneration.GenerateRandomKey(SecretBytes));

    public string BuildOtpAuthUri(string secret, string email)
    {
        var label = Uri.EscapeDataString($"{Issuer}:{email}");
        return $"otpauth://totp/{label}?secret={secret}&issuer={Uri.EscapeDataString(Issuer)}";
    }

    public long? ValidateCode(string secret, string code, DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(code))
            return null;

        // Só 6 dígitos ASCII — rejeita letras, espaços e tamanho errado antes de qualquer cálculo
        if (code.Length != CodeDigits || !code.All(char.IsAsciiDigit))
            return null;

        byte[] key;
        try
        {
            key = Base32Encoding.ToBytes(secret);
        }
        catch (ArgumentException)
        {
            return null;
        }

        var totp = new Totp(key, step: StepSeconds, totpSize: CodeDigits);
        var window = new VerificationWindow(previous: 1, future: 1);
        return totp.VerifyTotp(utcNow, code, out var matchedStep, window) ? matchedStep : null;
    }
}
