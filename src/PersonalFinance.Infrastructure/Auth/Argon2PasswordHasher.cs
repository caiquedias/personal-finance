using Konscious.Security.Cryptography;
using PersonalFinance.Domain.Interfaces.Services;
using System.Security.Cryptography;
using System.Text;

namespace PersonalFinance.Infrastructure.Auth;

/// <summary>
/// Implementação do IPasswordHasher usando Argon2id.
/// Parâmetros seguem as recomendações OWASP 2024:
///   - DegreeOfParallelism: 1
///   - MemorySize:          65536 (64 MB)
///   - Iterations:         3
///   - Salt:               16 bytes aleatórios por hash
///
/// Formato armazenado: Base64(salt) + ":" + Base64(hash)
/// Isso permite verificar sem armazenar o salt separadamente.
///
/// Dependência: Konscious.Security.Cryptography (NuGet)
/// </summary>
public sealed class Argon2PasswordHasher : IPasswordHasher
{
    // Parâmetros Argon2id — OWASP 2024
    private const int DegreeOfParallelism = 1;
    private const int MemorySize          = 65536; // 64 MB em KB
    private const int Iterations          = 3;
    private const int SaltSize            = 16;    // bytes
    private const int HashSize            = 32;    // bytes (256 bits)

    /// <summary>
    /// Gera hash Argon2id com salt aleatório.
    /// Retorna "Base64(salt):Base64(hash)".
    /// </summary>
    public string Hash(string plainPassword)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);

        using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(plainPassword))
        {
            Salt                  = salt,
            DegreeOfParallelism   = DegreeOfParallelism,
            MemorySize            = MemorySize,
            Iterations            = Iterations
        };

        var hash = argon2.GetBytes(HashSize);

        return $"{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
    }

    /// <summary>
    /// Verifica se a senha em texto plano corresponde ao hash armazenado.
    /// Usa comparação em tempo constante para evitar timing attacks.
    /// </summary>
    public bool Verify(string plainPassword, string storedHash)
    {
        var parts = storedHash.Split(':');
        if (parts.Length != 2) return false;

        byte[] salt;
        byte[] expectedHash;

        try
        {
            salt         = Convert.FromBase64String(parts[0]);
            expectedHash = Convert.FromBase64String(parts[1]);
        }
        catch
        {
            return false;
        }

        using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(plainPassword))
        {
            Salt                = salt,
            DegreeOfParallelism = DegreeOfParallelism,
            MemorySize          = MemorySize,
            Iterations          = Iterations
        };

        var actualHash = argon2.GetBytes(HashSize);

        // Comparação em tempo constante — evita timing attacks
        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }
}
