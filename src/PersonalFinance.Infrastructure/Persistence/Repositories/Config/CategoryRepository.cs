using Microsoft.EntityFrameworkCore;
using PersonalFinance.Domain.Entities.Config;
using PersonalFinance.Domain.Interfaces.Repositories;
using PersonalFinance.Infrastructure.Persistence.Context;

namespace PersonalFinance.Infrastructure.Persistence.Repositories.Config
{
    /// <summary>
    /// Implementação concreta de ICategoryRepository.
    /// IsAccessibleByUserAsync retorna true para categorias globais OU do próprio usuário.
    /// </summary>
    public sealed class CategoryRepository : ICategoryRepository
    {
        private readonly AppDbContext _context;

        public CategoryRepository(AppDbContext context) => _context = context;

        public async Task<Category?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => await _context.Categories.FirstOrDefaultAsync(c => c.Id == id, ct);

        public async Task<Category?> GetByIdAndUserAsync(
            Guid id, Guid userId, CancellationToken ct = default)
            => await _context.Categories
                   .FirstOrDefaultAsync(
                       c => c.Id == id && (c.UserId == userId || c.IsGlobal == false && c.UserId == userId),
                       ct);

        public async Task<IEnumerable<Category>> GetByUserAsync(
            Guid userId, CancellationToken ct = default)
            => await _context.Categories
                   .Where(c => c.UserId == userId || c.IsGlobal)
                   .ToListAsync(ct);

        public async Task<bool> ExistsByNameAndUserAsync(
            string name, Guid? userId, CancellationToken ct = default)
            => await _context.Categories
                   .AnyAsync(c => c.Name == name && c.UserId == userId, ct);

        public async Task<bool> IsAccessibleByUserAsync(
            Guid categoryId, Guid userId, CancellationToken ct = default)
            => await _context.Categories
                   .AnyAsync(c => c.Id == categoryId &&
                                  (c.IsGlobal || c.UserId == userId), ct);

        public async Task AddAsync(Category category, CancellationToken ct = default)
            => await _context.Categories.AddAsync(category, ct);

        public Task UpdateAsync(Category category, CancellationToken ct = default)
        {
            _context.Categories.Update(category);
            return Task.CompletedTask;
        }
    }
}
