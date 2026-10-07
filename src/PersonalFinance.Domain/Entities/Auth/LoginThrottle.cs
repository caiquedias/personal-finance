using PersonalFinance.Domain.Exceptions;

namespace PersonalFinance.Domain.Entities.Auth;

/// <summary>
/// Controle de tentativas de login por par (conta, IP) — uma linha por par (upsert), não por tentativa.
/// Não herda EntityBase: EXCEÇÃO deliberada à regra de soft-delete universal. É uma tabela efêmera
/// (retenção = janela do lockout; por LGPD o IP é dado pessoal) e a exclusão é FÍSICA.
/// </summary>
public sealed class LoginThrottle
{
    private const int MaxIpLength = 45;

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string IpAddress { get; private set; } = default!;
    public int FailedCount { get; private set; }
    public DateTime WindowStart { get; private set; }
    public DateTime? LockedUntil { get; private set; }

    /// <summary>Token de concorrência otimista (rowversion).</summary>
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    // ── EF Core ───────────────────────────────────────────────────────────────
    private LoginThrottle() { }

    public static LoginThrottle Create(Guid userId, string ipAddress, DateTime now)
    {
        if (userId == Guid.Empty)
            throw new DomainException("Usuário é obrigatório.");
        if (string.IsNullOrWhiteSpace(ipAddress) || ipAddress.Length > MaxIpLength)
            throw new DomainException("IP inválido.");

        return new LoginThrottle
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            IpAddress = ipAddress,
            FailedCount = 0,
            WindowStart = now
        };
    }

    /// <summary>
    /// Registra uma falha. <paramref name="window"/> é a janela de contagem e a duração do bloqueio.
    /// Reinicia a contagem se o bloqueio expirou ou se a janela passou sem bloqueio.
    /// </summary>
    public void RegisterFailure(int maxAttempts, TimeSpan window, DateTime now)
    {
        var lockExpired = LockedUntil.HasValue && now >= LockedUntil.Value;
        var windowExpired = !LockedUntil.HasValue && WindowStart + window < now;

        if (lockExpired || windowExpired)
        {
            FailedCount = 0;
            WindowStart = now;
            LockedUntil = null;
        }

        FailedCount++;
        if (FailedCount >= maxAttempts)
            LockedUntil = now + window;
    }

    public bool IsLockedOut(DateTime now) => LockedUntil.HasValue && now < LockedUntil.Value;

    public void Reset()
    {
        FailedCount = 0;
        LockedUntil = null;
    }

    /// <summary>Linha elegível para limpeza: janela vencida e sem bloqueio ativo.</summary>
    public bool IsExpired(DateTime now, TimeSpan window) =>
        WindowStart + window < now && (LockedUntil == null || LockedUntil < now);
}
