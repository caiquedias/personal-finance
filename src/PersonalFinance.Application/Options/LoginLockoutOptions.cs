namespace PersonalFinance.Application.Options
{
    /// <summary>
    /// Parâmetros do lockout temporário de conta no login (seção Auth:LoginLockout).
    /// </summary>
    public sealed class LoginLockoutOptions
    {
        /// <summary>Falhas consecutivas de senha que disparam o bloqueio.</summary>
        public int MaxFailedAttempts { get; set; } = 5;

        /// <summary>Duração do bloqueio em minutos.</summary>
        public int LockoutMinutes { get; set; } = 15;

        /// <summary>Teto global de falhas por conta (soma de todos os IPs) que bloqueia a conta.</summary>
        public int GlobalMaxFailedAttempts { get; set; } = 50;

        /// <summary>Teto duro de linhas na tabela de throttle por (conta, IP).</summary>
        public int ThrottleMaxRows { get; set; } = 2000;

        /// <summary>Tamanho do lote de limpeza de linhas expiradas antes de inserir um par novo.</summary>
        public int ThrottleCleanupBatchSize { get; set; } = 500;
    }
}
