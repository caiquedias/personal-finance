namespace PersonalFinance.Domain.Enums;

/// <summary>
/// Origem da despesa.
/// Parental = despesa relacionada aos pais (condomínio, financiamento, etc.)
/// Personal = despesa própria do usuário
/// </summary>
public enum SourceType
{
    Parental = 1,
    Personal = 2
}

/// <summary>
/// Quinzena de referência do lançamento.
/// First  = 1ª quinzena (dias 1–15)
/// Second = 2ª quinzena (dias 16–fim do mês)
/// </summary>
public enum FortnightType
{
    First  = 1,
    Second = 2
}

/// <summary>
/// Status de pagamento de uma despesa.
/// Extensível via módulo de configuração futuramente.
/// </summary>
public enum PaymentStatus
{
    Pending   = 1,
    Paid      = 2,
    Cancelled = 3,
    Partial   = 4
}

public enum ExpenseSortColumn
{
    Description      = 1,
    Category         = 2,
    Source           = 3,
    Fortnight        = 4,
    DueDate          = 5,
    Amount           = 6,
    Status           = 7,
    DragAndDropOrder = 8,
    UpdatedAt        = 9
}

public enum SortDirection
{
    Ascending  = 1,
    Descending = 2
}
