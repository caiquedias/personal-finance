namespace PersonalFinance.Domain.Exceptions;

/// <summary>
/// Exceção base para violações de regras de negócio do domínio.
/// Deve ser capturada na camada de Api e convertida em 400 Bad Request.
/// </summary>
public sealed class DomainException : Exception
{
    public DomainException(string message)
        : base(message) { }

    public DomainException(string message, Exception innerException)
        : base(message, innerException) { }
}
