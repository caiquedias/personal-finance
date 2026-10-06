namespace PersonalFinance.Application.Options;

/// <summary>Retenção do audit log (seção AuditLog:Retention).</summary>
public sealed class AuditLogRetentionOptions
{
    /// <summary>Quando false, o purge não executa.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Dias de retenção; registros mais antigos são removidos.</summary>
    public int RetentionDays { get; set; } = 365;

    /// <summary>Intervalo entre execuções do purge, em minutos.</summary>
    public int PurgeIntervalMinutes { get; set; } = 1440;

    /// <summary>Quantidade máxima de linhas removidas por lote.</summary>
    public int BatchSize { get; set; } = 1000;
}
