using FluentAssertions;
using Moq;
using PersonalFinance.Application.DTOs.Config;
using PersonalFinance.Application.DTOs.Financial;
using PersonalFinance.Application.Interfaces;
using PersonalFinance.Application.UseCases.Config;
using PersonalFinance.Application.UseCases.Financial.Periods;
using PersonalFinance.Domain.Entities.Config;
using PersonalFinance.Domain.Exceptions;
using PersonalFinance.Domain.Interfaces.Repositories;
using Xunit;

namespace PersonalFinance.Application.Tests.Unit.Config;

// ══════════════════════════════════════════════════════════════════════════════
// CATEGORY
// ══════════════════════════════════════════════════════════════════════════════

public class CreateCategoryUseCaseTests
{
    private readonly Mock<ICategoryRepository> _categoryRepo = new();
    private readonly Mock<IUnitOfWork>         _uow          = new();
    private readonly CreateCategoryUseCase     _sut;

    private static readonly Guid UserId = Guid.NewGuid();

    public CreateCategoryUseCaseTests() =>
        _sut = new CreateCategoryUseCase(_categoryRepo.Object, _uow.Object);

    [Fact(DisplayName = "Deve criar categoria de usuário com dados válidos")]
    public async Task Execute_WithValidData_ShouldCreateCategory()
    {
        _categoryRepo.Setup(r => r.ExistsByNameAndUserAsync("Moradia", UserId, default))
                     .ReturnsAsync(false);

        var result = await _sut.ExecuteAsync(
            new CreateCategoryDto("Moradia", "#1E4D2B", "home", UserId));

        result.Name.Should().Be("Moradia");
        result.IsGlobal.Should().BeFalse();
        _categoryRepo.Verify(r => r.AddAsync(It.IsAny<Category>(), default), Times.Once);
        _uow.Verify(u => u.CommitAsync(default), Times.Once);
    }

    [Fact(DisplayName = "Deve criar categoria global com UserId nulo")]
    public async Task Execute_GlobalCategory_ShouldHaveNullUserId()
    {
        _categoryRepo.Setup(r => r.ExistsByNameAndUserAsync("Global", null, default))
                     .ReturnsAsync(false);

        var result = await _sut.ExecuteAsync(
            new CreateCategoryDto("Global", "#FFFFFF", null, null, IsGlobal: true));

        result.IsGlobal.Should().BeTrue();
        result.UserId.Should().BeNull();
    }

    [Fact(DisplayName = "Deve lançar exceção para nome de categoria duplicado no mesmo usuário")]
    public async Task Execute_WithDuplicateName_ShouldThrow()
    {
        _categoryRepo.Setup(r => r.ExistsByNameAndUserAsync("Moradia", UserId, default))
                     .ReturnsAsync(true);

        var act = () => _sut.ExecuteAsync(
            new CreateCategoryDto("Moradia", "#FFFFFF", null, UserId));

        await act.Should().ThrowAsync<DomainException>()
                 .WithMessage("*categoria*");
        _uow.Verify(u => u.CommitAsync(default), Times.Never);
    }

    [Theory(DisplayName = "Deve lançar exceção para dados inválidos")]
    [MemberData(nameof(InvalidDtos))]
    public async Task Execute_WithInvalidData_ShouldThrow(CreateCategoryDto dto)
    {
        var act = () => _sut.ExecuteAsync(dto);
        await act.Should().ThrowAsync<Exception>();
    }

    public static IEnumerable<object[]> InvalidDtos() =>
    [
        [new CreateCategoryDto("",        "#FFFFFF", null, UserId)],
        [new CreateCategoryDto("Moradia", "FFFFFF",  null, UserId)],
        [new CreateCategoryDto("Moradia", "#GGG",    null, UserId)],
    ];
}

public class UpdateCategoryUseCaseTests
{
    private readonly Mock<ICategoryRepository> _categoryRepo = new();
    private readonly Mock<IUnitOfWork>         _uow          = new();
    private readonly UpdateCategoryUseCase     _sut;

    private static readonly Guid UserId     = Guid.NewGuid();
    private static readonly Guid CategoryId = Guid.NewGuid();

    public UpdateCategoryUseCaseTests() =>
        _sut = new UpdateCategoryUseCase(_categoryRepo.Object, _uow.Object);

    [Fact(DisplayName = "Deve atualizar categoria existente do usuário")]
    public async Task Execute_WithValidData_ShouldUpdate()
    {
        var category = Category.Create("Moradia", "#1E4D2B", "home", UserId);
        _categoryRepo.Setup(r => r.GetByIdAndUserAsync(CategoryId, UserId, default))
                     .ReturnsAsync(category);

        await _sut.ExecuteAsync(
            new UpdateCategoryDto(CategoryId, UserId, "Casa", "#FFFFFF", "house"));

        category.Name.Should().Be("Casa");
        _uow.Verify(u => u.CommitAsync(default), Times.Once);
    }

    [Fact(DisplayName = "Deve lançar exceção se categoria não for do usuário")]
    public async Task Execute_WithUnauthorizedCategory_ShouldThrow()
    {
        _categoryRepo.Setup(r => r.GetByIdAndUserAsync(CategoryId, UserId, default))
                     .ReturnsAsync((Category?)null);

        var act = () => _sut.ExecuteAsync(
            new UpdateCategoryDto(CategoryId, UserId, "Casa", "#FFFFFF", null));

        await act.Should().ThrowAsync<DomainException>()
                 .WithMessage("*categoria*");
    }
}

public class DeleteCategoryUseCaseTests
{
    private readonly Mock<ICategoryRepository> _categoryRepo = new();
    private readonly Mock<IExpenseRepository>  _expenseRepo  = new();
    private readonly Mock<IUnitOfWork>         _uow          = new();
    private readonly DeleteCategoryUseCase     _sut;

    private static readonly Guid UserId     = Guid.NewGuid();
    private static readonly Guid CategoryId = Guid.NewGuid();

    public DeleteCategoryUseCaseTests() =>
        _sut = new DeleteCategoryUseCase(_categoryRepo.Object, _expenseRepo.Object, _uow.Object);

    [Fact(DisplayName = "Deve fazer soft delete de categoria sem despesas vinculadas")]
    public async Task Execute_WithNoLinkedExpenses_ShouldSoftDelete()
    {
        var category = Category.Create("Moradia", "#1E4D2B", "home", UserId);
        _categoryRepo.Setup(r => r.GetByIdAndUserAsync(CategoryId, UserId, default))
                     .ReturnsAsync(category);
        _expenseRepo.Setup(r => r.HasExpensesByCategoryAsync(CategoryId, default))
                    .ReturnsAsync(false);

        await _sut.ExecuteAsync(CategoryId, UserId);

        category.IsDeleted.Should().BeTrue();
        _uow.Verify(u => u.CommitAsync(default), Times.Once);
    }

    [Fact(DisplayName = "Deve lançar exceção se categoria tiver despesas vinculadas")]
    public async Task Execute_WithLinkedExpenses_ShouldThrow()
    {
        var category = Category.Create("Moradia", "#1E4D2B", "home", UserId);
        _categoryRepo.Setup(r => r.GetByIdAndUserAsync(CategoryId, UserId, default))
                     .ReturnsAsync(category);
        _expenseRepo.Setup(r => r.HasExpensesByCategoryAsync(CategoryId, default))
                    .ReturnsAsync(true);

        var act = () => _sut.ExecuteAsync(CategoryId, UserId);

        await act.Should().ThrowAsync<DomainException>()
                 .WithMessage("*despesas*");
        _uow.Verify(u => u.CommitAsync(default), Times.Never);
    }
}
