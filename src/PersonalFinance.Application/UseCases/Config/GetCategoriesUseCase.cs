using PersonalFinance.Application.DTOs.Config;
using PersonalFinance.Domain.Entities.Config;
using PersonalFinance.Domain.Interfaces.Repositories;

namespace PersonalFinance.Application.UseCases.Config
{
    /// <summary>
    /// Retorna todas as categorias acessíveis ao usuário:
    /// categorias próprias + categorias globais (IsGlobal=true).
    /// </summary>
    public sealed class GetCategoriesUseCase
    {
        private readonly ICategoryRepository _repository;

        public GetCategoriesUseCase(ICategoryRepository repository)
            => _repository = repository;

        public async Task<IEnumerable<CategoryResponseDto>> ExecuteAsync(
            Guid userId, CancellationToken ct = default)
        {
            var categories = await _repository.GetByUserAsync(userId, ct);
            return categories.Select(ToDto);
        }

        private static CategoryResponseDto ToDto(Category c) =>
            new(c.Id, c.UserId, c.Name, c.Color, c.Icon, c.IsGlobal, c.IsActive);
    }
}
