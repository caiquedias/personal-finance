using PersonalFinance.Domain.Entities.Config;

namespace PersonalFinance.Domain.Interfaces.Repositories;

/// <summary>
/// Repositório de categorias.
/// IsAccessibleByUserAsync retorna true para categorias globais OU do próprio usuário.
/// </summary>
public interface ICategoryRepository
{
    Task<Category?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Category?> GetByIdAndUserAsync(Guid id, Guid userId, CancellationToken ct = default);
    Task<IEnumerable<Category>> GetByUserAsync(Guid userId, CancellationToken ct = default);
    Task<bool> ExistsByNameAndUserAsync(string name, Guid? userId, CancellationToken ct = default);
    Task<bool> IsAccessibleByUserAsync(Guid categoryId, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Retorna uma categoria global pelo nome exato (case-insensitive).
    /// Usado durante a importação para verificar se a categoria já existe.
    /// </summary>
    Task<Category?> GetGlobalByNameAsync(string name, CancellationToken ct = default);

    Task AddAsync(Category category, CancellationToken ct = default);
    Task UpdateAsync(Category category, CancellationToken ct = default);
}
