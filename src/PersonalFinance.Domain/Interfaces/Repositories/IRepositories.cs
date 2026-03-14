using PersonalFinance.Domain.Entities.Config;
using PersonalFinance.Domain.Entities.Financial;

namespace PersonalFinance.Domain.Interfaces.Repositories;

// ── Category ──────────────────────────────────────────────────────────────────

/// <summary>
/// Repositório de categorias.
/// IsAccessibleByUserAsync retorna true para categorias globais OU do próprio usuário.
/// </summary>
public interface ICategoryRepository
{
    Task<Category?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Retorna categoria se pertencer ao usuário OU for global.</summary>
    Task<Category?> GetByIdAndUserAsync(Guid id, Guid userId, CancellationToken ct = default);

    Task<IEnumerable<Category>> GetByUserAsync(Guid userId, CancellationToken ct = default);

    Task<bool> ExistsByNameAndUserAsync(string name, Guid? userId, CancellationToken ct = default);

    /// <summary>Retorna true se a categoria for global ou pertencer ao usuário informado.</summary>
    Task<bool> IsAccessibleByUserAsync(Guid categoryId, Guid userId, CancellationToken ct = default);

    Task AddAsync(Category category, CancellationToken ct = default);
    Task UpdateAsync(Category category, CancellationToken ct = default);
}

// ── Period ────────────────────────────────────────────────────────────────────

/// <summary>Repositório de períodos mensais.</summary>
public interface IPeriodRepository
{
    Task<Period?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Period?> GetByIdAndUserAsync(Guid id, Guid userId, CancellationToken ct = default);
    Task<Period?> GetByYearMonthAsync(Guid userId, int year, int month, CancellationToken ct = default);
    Task<IEnumerable<Period>> GetByUserAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Verifica se já existe um período para o usuário no mês/ano informado.</summary>
    Task<bool> ExistsAsync(Guid userId, int year, int month, CancellationToken ct = default);

    /// <summary>Verifica se o período existe e pertence ao usuário.</summary>
    Task<bool> ExistsByIdAndUserAsync(Guid periodId, Guid userId, CancellationToken ct = default);

    Task AddAsync(Period period, CancellationToken ct = default);
}

// ── Expense ───────────────────────────────────────────────────────────────────

/// <summary>Repositório de despesas.</summary>
public interface IExpenseRepository
{
    Task<Expense?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Expense?> GetByIdAndUserAsync(Guid id, Guid userId, CancellationToken ct = default);
    Task<IEnumerable<Expense>> GetByPeriodAsync(Guid periodId, Guid userId, CancellationToken ct = default);

    /// <summary>Verifica se existem despesas ativas vinculadas à categoria informada.</summary>
    Task<bool> HasExpensesByCategoryAsync(Guid categoryId, CancellationToken ct = default);

    Task AddAsync(Expense expense, CancellationToken ct = default);
    Task UpdateAsync(Expense expense, CancellationToken ct = default);
}

// ── Income ────────────────────────────────────────────────────────────────────

/// <summary>Repositório de receitas.</summary>
public interface IIncomeRepository
{
    Task<Income?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Income?> GetByIdAndUserAsync(Guid id, Guid userId, CancellationToken ct = default);
    Task<IEnumerable<Income>> GetByPeriodAsync(Guid periodId, Guid userId, CancellationToken ct = default);

    Task AddAsync(Income income, CancellationToken ct = default);
    Task UpdateAsync(Income income, CancellationToken ct = default);
}
