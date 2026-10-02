using PersonalFinance.Domain.Entities.Shared;
using PersonalFinance.Domain.Exceptions;

namespace PersonalFinance.Domain.Entities.Auth;

/// <summary>
/// Código de recuperação de uso único do MFA. Só o hash é guardado — o código em claro
/// é exibido ao usuário uma única vez, na ativação.
/// </summary>
public sealed class MfaRecoveryCode : EntityBase
{
    public Guid UserId { get; private set; }

    /// <summary>Hash do código (nunca o código em claro).</summary>
    public string CodeHash { get; private set; } = default!;

    /// <summary>Quando o código foi consumido (UTC). Null = ainda disponível.</summary>
    public DateTime? UsedAt { get; private set; }

    public bool IsUsed => UsedAt.HasValue;

    // ── EF Core ───────────────────────────────────────────────────────────────
    private MfaRecoveryCode() { }

    public static MfaRecoveryCode Create(Guid userId, string codeHash)
    {
        if (userId == Guid.Empty)
            throw new DomainException("Usuário é obrigatório.");
        if (string.IsNullOrWhiteSpace(codeHash))
            throw new DomainException("O hash do código de recuperação é obrigatório.");

        return new MfaRecoveryCode { UserId = userId, CodeHash = codeHash };
    }

    /// <summary>Consome o código. Código já usado não pode ser reutilizado.</summary>
    public void MarkUsed(DateTime now)
    {
        if (IsUsed)
            throw new DomainException("Código de recuperação já utilizado.");

        UsedAt = now;
        SetUpdatedAt();
    }
}
