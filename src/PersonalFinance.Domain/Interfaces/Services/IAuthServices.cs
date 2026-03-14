using PersonalFinance.Domain.Entities.Auth;

namespace PersonalFinance.Domain.Interfaces.Services;

/// <summary>
/// Serviço de hash de senha usando Argon2id.
/// Implementado em Infrastructure/Auth com Konscious.Security.Cryptography.
/// </summary>
public interface IPasswordHasher
{
    /// <summary>Gera hash Argon2id da senha em texto plano.</summary>
    string Hash(string plainPassword);

    /// <summary>Verifica se a senha em texto plano corresponde ao hash armazenado.</summary>
    bool Verify(string plainPassword, string storedHash);
}
