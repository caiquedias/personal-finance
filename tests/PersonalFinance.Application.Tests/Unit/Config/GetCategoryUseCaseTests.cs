using FluentAssertions;
using Moq;
using PersonalFinance.Application.UseCases.Config;
using PersonalFinance.Domain.Entities.Config;
using PersonalFinance.Domain.Interfaces.Repositories;
using Xunit;

namespace PersonalFinance.Application.Tests.Unit.Config;

public class GetCategoriesUseCaseTests
{
    private readonly Mock<ICategoryRepository> _repo = new();
    private readonly GetCategoriesUseCase      _sut;

    public GetCategoriesUseCaseTests()
        => _sut = new GetCategoriesUseCase(_repo.Object);

    [Fact(DisplayName = "Deve retornar categorias do usuário e globais")]
    public async Task Execute_ShouldReturnCategories()
    {
        var userId = Guid.NewGuid();
        var categories = new List<Category>
        {
            Category.Create("Alimentação", "#FF0000", null, userId),
            Category.CreateGlobal("Moradia", "#00FF00", null)
        };
        _repo.Setup(r => r.GetByUserAsync(userId, default)).ReturnsAsync(categories);

        var result = await _sut.ExecuteAsync(userId);

        result.Should().HaveCount(2);
    }

    [Fact(DisplayName = "Deve retornar lista vazia se não houver categorias")]
    public async Task Execute_WithNoCategories_ShouldReturnEmpty()
    {
        var userId = Guid.NewGuid();
        _repo.Setup(r => r.GetByUserAsync(userId, default)).ReturnsAsync([]);

        var result = await _sut.ExecuteAsync(userId);

        result.Should().BeEmpty();
    }
}

public class GetCategoryByIdUseCaseTests
{
    private readonly Mock<ICategoryRepository> _repo = new();
    private readonly GetCategoryByIdUseCase    _sut;

    public GetCategoryByIdUseCaseTests()
        => _sut = new GetCategoryByIdUseCase(_repo.Object);

    [Fact(DisplayName = "Deve retornar categoria pelo Id")]
    public async Task Execute_WithExistingCategory_ShouldReturn()
    {
        var userId     = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var category   = Category.Create("Lazer", "#0000FF", null, userId);

        _repo.Setup(r => r.GetByIdAndUserAsync(categoryId, userId, default)).ReturnsAsync(category);

        var result = await _sut.ExecuteAsync(categoryId, userId);

        result.Name.Should().Be("Lazer");
    }

    [Fact(DisplayName = "Deve lançar exceção se categoria não encontrada")]
    public async Task Execute_WithNotFoundCategory_ShouldThrow()
    {
        var categoryId = Guid.NewGuid();
        var userId     = Guid.NewGuid();
        _repo.Setup(r => r.GetByIdAndUserAsync(categoryId, userId, default)).ReturnsAsync((Category?)null);

        var act = () => _sut.ExecuteAsync(categoryId, userId);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }
}
