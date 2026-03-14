namespace PersonalFinance.Domain.Entities.Lookup;

/// <summary>
/// Tabela de perfis de acesso — seed fixo, sem EntityBase.
/// Valores gerenciados pelo sistema, não pelo usuário.
/// </summary>
public sealed class Role
{
    public int    Id          { get; set; }
    public string Name        { get; set; } = default!;
    public string Description { get; set; } = default!;
}

/// <summary>
/// Tipo de origem da despesa: Parental (pais) ou Personal (própria).
/// Seed fixo — extensível futuramente via módulo de configuração.
/// </summary>
public sealed class SourceType
{
    public int    Id   { get; set; }
    public string Name { get; set; } = default!;
}

/// <summary>
/// Quinzena de referência: First (dias 1–15) ou Second (dias 16–fim).
/// Seed fixo.
/// </summary>
public sealed class FortnightType
{
    public int    Id   { get; set; }
    public string Name { get; set; } = default!;
}

/// <summary>
/// Status de pagamento de uma despesa.
/// Inicialmente seed fixo — módulo de configuração permitirá customização futura.
/// </summary>
public sealed class PaymentStatus
{
    public int    Id          { get; set; }
    public string Name        { get; set; } = default!;
    public string Description { get; set; } = default!;
}
