using PersonalFinance.Application.DTOs.Financial;

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
}
