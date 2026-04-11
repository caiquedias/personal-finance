namespace PersonalFinance.Application.DTOs;

/// <summary>
/// Resultado paginado genérico — reutilizável em qualquer listagem com paginação server-side.
/// </summary>
public sealed record PagedResult<T>(
    IEnumerable<T> Items,
    int TotalCount,
    int PageNumber,
    int PageSize);
