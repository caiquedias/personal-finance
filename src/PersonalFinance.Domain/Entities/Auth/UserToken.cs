using PersonalFinance.Domain.Enums;
using PersonalFinance.Domain.Exceptions;

namespace PersonalFinance.Domain.Entities.Auth;

/// <summary>
/// Código de uso único (reset de senha / verificação de e-mail). Só o hash é guardado.
/// Não herda EntityBase: EXCEÇÃO deliberada ao soft-delete — os tokens anteriores do mesmo
/// (usuário, propósito) são removidos fisicamente a cada nova emissão.
/// </summary>
public sealed class UserToken
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public UserTokenPurpose Purpose { get; private set; }

    /// <summary>HMAC do código (nunca o código em claro).</summary>
    public string TokenHash { get; private set; } = default!;

    public DateTime CreatedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public DateTime? UsedAt { get; private set; }

    /// <summary>Quantidade de tentativas erradas.</summary>
    public int Attempts { get; private set; }

    /// <summary>True quando o limite de tentativas erradas foi atingido.</summary>
    public bool IsInvalidated { get; private set; }

    /// <summary>Token de concorrência otimista (rowversion) — protege o contador de tentativas.</summary>
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    // ── EF Core ───────────────────────────────────────────────────────────────
    private UserToken() { }

    public static UserToken Create(Guid userId, UserTokenPurpose purpose, DateTime now, TimeSpan ttl)
    {
        if (userId == Guid.Empty)
            throw new DomainException("Usuário é obrigatório.");
        if (!Enum.IsDefined(purpose))
            throw new DomainException("Finalidade do token inválida.");
        if (ttl <= TimeSpan.Zero)
            throw new DomainException("O tempo de vida do token deve ser positivo.");

        return new UserToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Purpose = purpose,
            CreatedAt = now,
            ExpiresAt = now + ttl
        };
    }

    /// <summary>Define o hash (depende do Id, por isso separado do Create).</summary>
    public void SetTokenHash(string tokenHash)
    {
        if (string.IsNullOrWhiteSpace(tokenHash))
            throw new DomainException("O hash do token é obrigatório.");

        TokenHash = tokenHash;
    }

    /// <summary>Utilizável: não usado, não invalidado e antes da expiração (now &gt;= ExpiresAt expira).</summary>
    public bool IsUsable(DateTime now) => UsedAt == null && !IsInvalidated && now < ExpiresAt;

    /// <summary>Registra tentativa errada; ao atingir o máximo, o token é invalidado.</summary>
    public void RegisterFailedAttempt(int maxAttempts)
    {
        if (maxAttempts < 1)
            throw new DomainException("O máximo de tentativas deve ser ao menos 1.");

        Attempts++;
        if (Attempts >= maxAttempts)
            IsInvalidated = true;
    }

    /// <summary>Consome o token (uso único).</summary>
    public void MarkUsed(DateTime now)
    {
        if (UsedAt.HasValue)
            throw new DomainException("O token já foi utilizado.");

        UsedAt = now;
    }
}
