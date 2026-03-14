using PersonalFinance.Application.DTOs.Config;
using PersonalFinance.Domain.Entities.Config;
using PersonalFinance.Domain.Exceptions;
using PersonalFinance.Domain.Interfaces.Repositories;

namespace PersonalFinance.Application.UseCases.Config;

// ══════════════════════════════════════════════════════════════════════════════
// CREATE CATEGORY
// ══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Cria uma nova categoria — de usuário ou global (admin).
/// Valida unicidade do nome no escopo do usuário (ou global para categorias globais).
/// </summary>
public sealed class CreateCategoryUseCase
{
    private readonly ICategoryRepository _categoryRepository;
    private readonly IUnitOfWork         _unitOfWork;

    public CreateCategoryUseCase(
        ICategoryRepository categoryRepository,
        IUnitOfWork         unitOfWork)
    {
        _categoryRepository = categoryRepository;
        _unitOfWork         = unitOfWork;
    }

    public async Task<CategoryResponseDto> ExecuteAsync(
        CreateCategoryDto dto,
        CancellationToken ct = default)
    {
        // Verifica duplicidade de nome no escopo do usuário (ou global)
        var exists = await _categoryRepository
            .ExistsByNameAndUserAsync(dto.Name, dto.UserId, ct);

        if (exists)
            throw new DomainException(
                $"Já existe uma categoria com o nome '{dto.Name}' neste escopo.");

        var category = dto.IsGlobal
            ? Category.CreateGlobal(dto.Name, dto.Color, dto.Icon)
            : Category.Create(dto.Name, dto.Color, dto.Icon, dto.UserId!.Value);

        await _categoryRepository.AddAsync(category, ct);
        await _unitOfWork.CommitAsync(ct);

        return ToDto(category);
    }

    private static CategoryResponseDto ToDto(Category c) =>
        new(c.Id, c.UserId, c.Name, c.Color, c.Icon, c.IsGlobal, c.IsActive);
}

// ══════════════════════════════════════════════════════════════════════════════
// UPDATE CATEGORY
// ══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Atualiza nome, cor e ícone de uma categoria existente do usuário.
/// Categorias globais não podem ser editadas por usuários comuns.
/// </summary>
public sealed class UpdateCategoryUseCase
{
    private readonly ICategoryRepository _categoryRepository;
    private readonly IUnitOfWork         _unitOfWork;

    public UpdateCategoryUseCase(
        ICategoryRepository categoryRepository,
        IUnitOfWork         unitOfWork)
    {
        _categoryRepository = categoryRepository;
        _unitOfWork         = unitOfWork;
    }

    public async Task ExecuteAsync(
        UpdateCategoryDto dto,
        CancellationToken ct = default)
    {
        var category = await _categoryRepository
            .GetByIdAndUserAsync(dto.Id, dto.UserId, ct);

        if (category is null)
            throw new DomainException(
                "Categoria não encontrada ou sem permissão de acesso.");

        category.Update(dto.Name, dto.Color, dto.Icon);

        await _categoryRepository.UpdateAsync(category, ct);
        await _unitOfWork.CommitAsync(ct);
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// DELETE CATEGORY
// ══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Realiza soft delete de uma categoria.
/// Bloqueia a exclusão se existirem despesas ativas vinculadas.
/// </summary>
public sealed class DeleteCategoryUseCase
{
    private readonly ICategoryRepository _categoryRepository;
    private readonly IExpenseRepository  _expenseRepository;
    private readonly IUnitOfWork         _unitOfWork;

    public DeleteCategoryUseCase(
        ICategoryRepository categoryRepository,
        IExpenseRepository  expenseRepository,
        IUnitOfWork         unitOfWork)
    {
        _categoryRepository = categoryRepository;
        _expenseRepository  = expenseRepository;
        _unitOfWork         = unitOfWork;
    }

    public async Task ExecuteAsync(
        Guid categoryId,
        Guid userId,
        CancellationToken ct = default)
    {
        var category = await _categoryRepository
            .GetByIdAndUserAsync(categoryId, userId, ct);

        if (category is null)
            throw new DomainException(
                "Categoria não encontrada ou sem permissão de acesso.");

        // Impede exclusão de categorias que possuem despesas vinculadas
        var hasExpenses = await _expenseRepository
            .HasExpensesByCategoryAsync(categoryId, ct);

        if (hasExpenses)
            throw new DomainException(
                "Não é possível excluir a categoria pois existem despesas vinculadas a ela.");

        category.SoftDelete();

        await _categoryRepository.UpdateAsync(category, ct);
        await _unitOfWork.CommitAsync(ct);
    }
}
