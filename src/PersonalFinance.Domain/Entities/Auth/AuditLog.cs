using PersonalFinance.Domain.Enums;
using PersonalFinance.Domain.Exceptions;

namespace PersonalFinance.Domain.Entities.Auth;

/// <summary>
/// Trilha de auditoria de ações administrativas — insert-only.
/// Não herda EntityBase: EXCEÇÃO deliberada ao soft-delete universal (registro imutável; a remoção
/// ocorre apenas por purge de retenção). Details nunca deve conter senha, hash, secret MFA nem e-mail.
/// </summary>
public sealed class AuditLog
{
    public const int MaxDetailsLength = 2000;
    public const int MaxIpLength = 45;

    public Guid Id { get; private set; }
    public Guid ActorUserId { get; private set; }
    public AuditAction Action { get; private set; }
    public Guid TargetUserId { get; private set; }
    public string? Details { get; private set; }
    public string? IpAddress { get; private set; }
    public DateTime CreatedAt { get; private set; }

    // ── EF Core ───────────────────────────────────────────────────────────────
    private AuditLog() { }

    public static AuditLog Create(
        Guid actorUserId, AuditAction action, Guid targetUserId, string? details, string? ipAddress, DateTime now)
    {
        if (actorUserId == Guid.Empty)
            throw new DomainException("Ator da auditoria é obrigatório.");
        if (targetUserId == Guid.Empty)
            throw new DomainException("Alvo da auditoria é obrigatório.");
        if (details is { Length: > MaxDetailsLength })
            throw new DomainException($"Detalhes da auditoria excedem {MaxDetailsLength} caracteres.");
        if (ipAddress is { Length: > MaxIpLength })
            throw new DomainException("IP inválido.");

        return new AuditLog
        {
            Id = Guid.NewGuid(),
            ActorUserId = actorUserId,
            Action = action,
            TargetUserId = targetUserId,
            Details = details,
            IpAddress = ipAddress,
            CreatedAt = now
        };
    }
}
