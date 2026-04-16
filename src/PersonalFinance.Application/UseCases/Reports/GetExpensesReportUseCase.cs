using PersonalFinance.Application.DTOs.Reports;
using PersonalFinance.Application.Interfaces;

namespace PersonalFinance.Application.UseCases.Reports;

/// <summary>
/// Retorna despesas agrupadas por categoria para o ano (e opcionalmente mês) informado.
/// </summary>
public sealed class GetExpensesReportUseCase
{
    private readonly IReportRepository _repository;

    public GetExpensesReportUseCase(IReportRepository repository)
        => _repository = repository;

    public Task<ExpensesReportDto> ExecuteAsync(
        Guid userId,
        int year,
        int? month,
        CancellationToken ct = default)
        => _repository.GetExpensesReportAsync(userId, year, month, ct);
}
