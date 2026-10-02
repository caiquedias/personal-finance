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
    }
}
