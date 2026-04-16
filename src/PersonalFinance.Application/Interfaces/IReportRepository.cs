using PersonalFinance.Application.DTOs.Financial;
using PersonalFinance.Application.DTOs.Reports;

namespace PersonalFinance.Application.Interfaces;

/// <summary>
/// Repositório somente leitura para relatórios e a vw_PeriodSummary.
/// Definido na camada Application (não Domain) porque retorna DTOs de Application,
/// evitando dependência circular Domain → Application.
/// Implementado na Infrastructure via projeção EF Core direto na view.
/// </summary>
public interface IReportRepository
{
    /// <summary>
    /// Retorna o resumo financeiro do período: receitas, despesas, pago, devedor e saldo.
    /// NULL se o período não existir ou não pertencer ao usuário.
    /// </summary>
    Task<PeriodSummaryDto?> GetPeriodSummaryAsync(
        Guid periodId,
        Guid userId,
        CancellationToken ct = default);

    /// <summary>
    /// Agrega despesas por categoria para o ano informado.
    /// Se month for informado, filtra pelo mês específico.
    /// </summary>
    Task<ExpensesReportDto> GetExpensesReportAsync(
        Guid userId,
        int year,
        int? month,
        CancellationToken ct = default);
}
