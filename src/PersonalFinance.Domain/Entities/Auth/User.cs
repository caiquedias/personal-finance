using PersonalFinance.Domain.Entities.Shared;
using PersonalFinance.Domain.Exceptions;
using System.Text.RegularExpressions;

namespace PersonalFinance.Domain.Entities.Auth;

/// <summary>
/// Usuário autenticado do sistema.
/// Senhas nunca são armazenadas em texto plano — apenas o hash Argon2id.
/// E-mail é normalizado para lowercase na criação e atualização.
/// </summary>
public sealed class User : EntityBase
{
    private static readonly Regex EmailRegex = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // ── Propriedades ──────────────────────────────────────────────────────────

    /// <summary>Nome completo do usuário.</summary>
    public string Name { get; private set; } = default!;

    /// <summary>E-mail único — normalizado para lowercase. Usado no login.</summary>
    public string Email { get; private set; } = default!;

    /// <summary>Hash Argon2id da senha. Nunca exposto em DTOs de resposta.</summary>
    public string PasswordHash { get; private set; } = default!;

    // ── EF Core ───────────────────────────────────────────────────────────────
    private User() { }

    // ── Factory ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Cria um novo usuário validando todos os invariantes de domínio.
    /// O hash da senha deve ser gerado antes de chamar este método (Argon2id).
    /// </summary>
    public static User Create(string name, string email, string passwordHash)
    {
        ValidateName(name);
        ValidateEmail(email);
        ValidatePasswordHash(passwordHash);

        return new User
        {
            Name         = name.Trim(),
            Email        = email.Trim().ToLowerInvariant(),
            PasswordHash = passwordHash
        };
    }

    // ── Comportamentos ────────────────────────────────────────────────────────

    /// <summary>Atualiza o nome do usuário.</summary>
    public void UpdateName(string name)
    {
        ValidateName(name);
        Name = name.Trim();
        SetUpdatedAt();
    }

    /// <summary>
    /// Substitui o hash da senha.
    /// Deve ser chamado apenas após gerar novo hash Argon2id.
    /// </summary>
    public void UpdatePasswordHash(string passwordHash)
    {
        ValidatePasswordHash(passwordHash);
        PasswordHash = passwordHash;
        SetUpdatedAt();
    }

    // ── Validações privadas ───────────────────────────────────────────────────

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("O nome do usuário é obrigatório.");

        if (name.Trim().Length > 100)
            throw new DomainException("O nome do usuário não pode exceder 100 caracteres.");
    }

    private static void ValidateEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new DomainException("O e-mail do usuário é obrigatório.");

        var trimmed = email.Trim();

        if (trimmed.Length > 200)
            throw new DomainException("O e-mail não pode exceder 200 caracteres.");

        if (!EmailRegex.IsMatch(trimmed))
            throw new DomainException($"O e-mail '{trimmed}' não é válido.");
    }

    private static void ValidatePasswordHash(string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new DomainException("O hash da senha é obrigatório.");
    }
}
