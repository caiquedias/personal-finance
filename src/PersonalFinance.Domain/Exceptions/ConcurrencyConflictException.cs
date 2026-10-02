namespace PersonalFinance.Domain.Exceptions;

/// <summary>
/// Conflito de concorrência otimista ao persistir (outra requisição alterou a mesma linha).
/// Lançada pela UnitOfWork para não vazar tipos do EF Core para a camada Application.
/// </summary>
public sealed class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException()
        : base("Conflito de concorrência ao persistir as alterações.") { }

    public ConcurrencyConflictException(Exception innerException)
        : base("Conflito de concorrência ao persistir as alterações.", innerException) { }
}
