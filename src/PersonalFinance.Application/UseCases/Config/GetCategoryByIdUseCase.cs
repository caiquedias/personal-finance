using PersonalFinance.Application.DTOs.Config;
using PersonalFinance.Domain.Interfaces.Repositories;

namespace PersonalFinance.Application.UseCases.Config
{
    /// <summary>
    /// Retorna uma categoria específica acessível ao usuário (própria ou global).
    /// </summary>
    public sealed class GetCategoryByIdUseCase
    {
        private readonly ICategoryRepository _repository;

        public GetCategoryByIdUseCase(ICategoryRepository repository)
            => _repository = repository;

        public async Task<CategoryResponseDto> ExecuteAsync(
            Guid categoryId, Guid userId, CancellationToken ct = default)
        {
            var category = await _repository.GetByIdAndUserAsync(categoryId, userId, ct)
                ?? throw new KeyNotFoundException("Categoria não encontrada.");

            return new CategoryResponseDto(
                category.Id, category.UserId, category.Name,
                category.Color, category.Icon, category.IsGlobal, category.IsActive);
        }
    }
}
