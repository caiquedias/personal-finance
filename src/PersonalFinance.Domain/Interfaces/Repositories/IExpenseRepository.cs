using PersonalFinance.Domain.Entities.Financial;
using PersonalFinance.Domain.Enums;

namespace PersonalFinance.Domain.Interfaces.Repositories;

public interface IExpenseRepository
{
    Task<Expense?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Expense?> GetByIdAndUserAsync(Guid id, Guid userId, CancellationToken ct = default);
    Task<IEnumerable<Expense>> GetByPeriodAsync(Guid periodId, Guid userId, CancellationToken ct = default);

    Task<(IEnumerable<Expense> Items, int TotalCount)> GetPagedByPeriodAsync(
        Guid          periodId,
        Guid          userId,
        int           pageNumber,
        int           pageSize,
        string?       description,
        Guid?         categoryId,
        PaymentStatus? paymentStatus,
        FortnightType? fortnightType,
        CancellationToken ct = default);

    /// <summary>
    /// Busca uma despesa pela chave de idempotência de importação:
    /// PeriodId + Descrição (case-insensitive) + Valor + Quinzena.
    /// Usado para upsert inteligente durante re-importação da planilha legada.
    /// </summary>
    Task<Expense?> FindByImportKeyAsync(
        Guid          periodId,
        string        description,
        decimal       amount,
        FortnightType fortnightType,
        CancellationToken ct = default);

    Task AddAsync(Expense expense, CancellationToken ct = default);
    Task UpdateAsync(Expense expense, CancellationToken ct = default);

    /// <summary>
    /// Retorna true se existir qualquer despesa ativa vinculada à categoria informada.
    /// Usado pelo DeleteCategoryUseCase para impedir exclusão com dependências.
    /// </summary>
    Task<bool> HasExpensesByCategoryAsync(Guid categoryId, CancellationToken ct = default);
}
