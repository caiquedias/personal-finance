using Microsoft.Extensions.Options;
using PersonalFinance.Application.Interfaces;
using PersonalFinance.Application.Options;
using PersonalFinance.Domain.Enums;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace PersonalFinance.Infrastructure.Auth;

/// <summary>
/// Códigos de uso único: 6 dígitos via CSPRNG e HMAC-SHA256 com chave do servidor sobre
/// tokenId|userId|purpose|código. Comparação em tempo fixo.
/// </summary>
public sealed class HmacOneTimeCodeService : IOneTimeCodeService
{
    private const int KeySizeBytes = 32;
    private readonly byte[] _key;

    public HmacOneTimeCodeService(IOptions<UserTokenOptions> options)
    {
        var raw = options.Value.HmacKey;
        if (string.IsNullOrWhiteSpace(raw))
            throw new InvalidOperationException("Auth:UserTokens:HmacKey é obrigatória.");

        var buffer = new byte[KeySizeBytes + 8];
        if (!Convert.TryFromBase64String(raw, buffer, out var written))
            throw new InvalidOperationException("Auth:UserTokens:HmacKey deve ser Base64 de 32 bytes.");
        if (written != KeySizeBytes)
            throw new InvalidOperationException("Auth:UserTokens:HmacKey deve ter exatamente 32 bytes.");

        _key = buffer[..written];
    }

    public string GenerateCode()
        => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", CultureInfo.InvariantCulture);

    public string ComputeHash(Guid tokenId, Guid userId, UserTokenPurpose purpose, string code)
    {
        var payload = $"{tokenId:D}|{userId:D}|{(int)purpose}|{code}";
        return Convert.ToBase64String(HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(payload)));
    }

    public bool Verify(Guid tokenId, Guid userId, UserTokenPurpose purpose, string code, string expectedHash)
    {
        if (string.IsNullOrEmpty(code) || string.IsNullOrWhiteSpace(expectedHash))
            return false;

        var computed = Encoding.UTF8.GetBytes(ComputeHash(tokenId, userId, purpose, code));
        var expected = Encoding.UTF8.GetBytes(expectedHash);
        return CryptographicOperations.FixedTimeEquals(computed, expected);
    }
}
