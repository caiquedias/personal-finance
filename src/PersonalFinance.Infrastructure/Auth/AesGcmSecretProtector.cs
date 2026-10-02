using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using PersonalFinance.Application.Options;
using PersonalFinance.Domain.Interfaces.Services;

namespace PersonalFinance.Infrastructure.Auth;

/// <summary>
/// Cifra segredos em repouso com AES-256-GCM. Chave em Auth:Mfa:EncryptionKey (Base64 de 32 bytes).
/// Nonce aleatório de 12 bytes por registro; formato "b64(nonce)|b64(cipher)|b64(tag)".
/// </summary>
public sealed class AesGcmSecretProtector : ISecretProtector
{
    private const int KeySize = 32;
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const char Separator = '|';

    private readonly byte[] _key;

    public AesGcmSecretProtector(IOptions<MfaOptions> options)
        => _key = ParseKey(options.Value.EncryptionKey);

    /// <summary>
    /// Valida e converte a chave sem lançar para entrada inválida. Usado também na validação do startup.
    /// </summary>
    public static bool TryParseKey(string? base64, out byte[] key)
    {
        key = Array.Empty<byte>();
        if (string.IsNullOrWhiteSpace(base64))
            return false;

        var trimmed = base64.Trim();
        var buffer = new byte[trimmed.Length];
        if (!Convert.TryFromBase64String(trimmed, buffer, out var written) || written != KeySize)
            return false;

        key = buffer[..written];
        return true;
    }

    public string Protect(string plain)
    {
        var plainBytes = Encoding.UTF8.GetBytes(plain);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var cipher = new byte[plainBytes.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, plainBytes, cipher, tag);

        return string.Join(Separator,
            Convert.ToBase64String(nonce), Convert.ToBase64String(cipher), Convert.ToBase64String(tag));
    }

    public string Unprotect(string value)
    {
        var parts = value?.Split(Separator);
        if (parts is not { Length: 3 })
            throw new CryptographicException("Formato de segredo protegido inválido.");

        byte[] nonce, cipher, tag;
        try
        {
            nonce = Convert.FromBase64String(parts[0]);
            cipher = Convert.FromBase64String(parts[1]);
            tag = Convert.FromBase64String(parts[2]);
        }
        catch (FormatException)
        {
            throw new CryptographicException("Formato de segredo protegido inválido.");
        }

        if (nonce.Length != NonceSize || tag.Length != TagSize)
            throw new CryptographicException("Formato de segredo protegido inválido.");

        var plain = new byte[cipher.Length];
        using var aes = new AesGcm(_key, TagSize);
        aes.Decrypt(nonce, cipher, tag, plain);

        return Encoding.UTF8.GetString(plain);
    }

    private static byte[] ParseKey(string? base64)
    {
        if (!TryParseKey(base64, out var key))
            throw new CryptographicException("Auth:Mfa:EncryptionKey deve ser Base64 de exatamente 32 bytes.");
        return key;
    }
}
