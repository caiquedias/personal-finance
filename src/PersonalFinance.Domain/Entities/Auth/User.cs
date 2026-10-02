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

    /// <summary>Quantidade de falhas de login consecutivas.</summary>
    public int FailedLoginCount { get; private set; }

    /// <summary>Fim do bloqueio de login (UTC). Null quando não bloqueado.</summary>

    /// <summary>Token de concorrência otimista (rowversion) — protege o contador de falhas de login.</summary>
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();
    public DateTime? LockedUntil { get; private set; }

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

    /// <summary>Indica se a conta está bloqueada em <paramref name="now"/> (expira quando now >= LockedUntil).</summary>
    public bool IsLockedOut(DateTime now) => LockedUntil.HasValue && now < LockedUntil.Value;

    /// <summary>
    /// Registra falha de login. Se um bloqueio anterior já expirou, o contador reinicia antes de incrementar.
    /// Ao atingir o limite, bloqueia até now + duração.
    /// </summary>
    public void RegisterFailedLogin(int maxAttempts, TimeSpan lockoutDuration, DateTime now)
    {
        if (LockedUntil.HasValue && now >= LockedUntil.Value)
        {
            FailedLoginCount = 0;
            LockedUntil = null;
        }

        FailedLoginCount++;

        if (FailedLoginCount >= maxAttempts)
            LockedUntil = now + lockoutDuration;

        SetUpdatedAt();
    }

    /// <summary>Zera o contador de falhas e remove o bloqueio.</summary>
    public void ResetFailedLogins()
    {
        FailedLoginCount = 0;
        LockedUntil = null;
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
