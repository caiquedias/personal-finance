using PersonalFinance.Application.DTOs.Financial;
using PersonalFinance.Domain.Entities.Financial;
using PersonalFinance.Domain.Enums;
using PersonalFinance.Domain.Exceptions;
using PersonalFinance.Domain.Interfaces.Repositories;

namespace PersonalFinance.Application.UseCases.Financial.Expenses;

// ══════════════════════════════════════════════════════════════════════════════
// CREATE EXPENSE
// ══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Registra uma nova despesa em um período existente.
/// Valida posse do período e acessibilidade da categoria (própria ou global).
/// </summary>
public sealed class CreateExpenseUseCase
{
    private readonly IExpenseRepository  _expenseRepository;
    private readonly IPeriodRepository   _periodRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IUnitOfWork         _unitOfWork;

    public CreateExpenseUseCase(
        IExpenseRepository  expenseRepository,
        IPeriodRepository   periodRepository,
        ICategoryRepository categoryRepository,
        IUnitOfWork         unitOfWork)
    {
        _expenseRepository  = expenseRepository;
        _periodRepository   = periodRepository;
        _categoryRepository = categoryRepository;
        _unitOfWork         = unitOfWork;
    }

    public async Task<ExpenseResponseDto> ExecuteAsync(
        CreateExpenseDto dto,
        CancellationToken ct = default)
    {
        // Garante que o período existe e pertence ao usuário
        var periodExists = await _periodRepository
            .ExistsByIdAndUserAsync(dto.PeriodId, dto.UserId, ct);

        if (!periodExists)
            throw new DomainException(
                "Período não encontrado ou sem permissão de acesso.");

        // Garante que a categoria é acessível (própria do usuário ou global)
        var categoryAccessible = await _categoryRepository
            .IsAccessibleByUserAsync(dto.CategoryId, dto.UserId, ct);

        if (!categoryAccessible)
            throw new DomainException(
                "Categoria não encontrada ou sem permissão de acesso.");

        var expense = Expense.Create(
            periodId:      dto.PeriodId,
            userId:        dto.UserId,
            categoryId:    dto.CategoryId,
            sourceType:    dto.SourceType,
            fortnightType: dto.FortnightType,
            paymentStatus: PaymentStatus.Pending,
            description:   dto.Description,
            amount:        dto.Amount,
            dueDate:       dto.DueDate,
            paymentDate:   null,
            notes:         dto.Notes
        );

        await _expenseRepository.AddAsync(expense, ct);
        await _unitOfWork.CommitAsync(ct);

        return ToDto(expense);
    }

    private static ExpenseResponseDto ToDto(Expense e) =>
        new(e.Id, e.PeriodId, e.UserId, e.CategoryId,
            e.SourceType, e.FortnightType, e.PaymentStatus,
            e.Description, e.Amount, e.DueDate, e.PaymentDate,
            e.Notes, e.IsActive);
}

// ══════════════════════════════════════════════════════════════════════════════
// UPDATE EXPENSE
// ══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Atualiza os dados editáveis de uma despesa existente, incluindo status de pagamento.
/// </summary>
public sealed class UpdateExpenseUseCase
{
    private readonly IExpenseRepository  _expenseRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IUnitOfWork         _unitOfWork;

    public UpdateExpenseUseCase(
        IExpenseRepository  expenseRepository,
        ICategoryRepository categoryRepository,
        IUnitOfWork         unitOfWork)
    {
        _expenseRepository  = expenseRepository;
        _categoryRepository = categoryRepository;
        _unitOfWork         = unitOfWork;
    }

    public async Task ExecuteAsync(
        UpdateExpenseDto dto,
        CancellationToken ct = default)
    {
        var expense = await _expenseRepository
            .GetByIdAndUserAsync(dto.Id, dto.UserId, ct);

        if (expense is null)
            throw new DomainException(
                "Despesa não encontrada ou sem permissão de acesso.");

        // Valida nova categoria se for diferente da atual
        if (dto.CategoryId != expense.CategoryId)
        {
            var categoryAccessible = await _categoryRepository
                .IsAccessibleByUserAsync(dto.CategoryId, dto.UserId, ct);

            if (!categoryAccessible)
                throw new DomainException(
                    "Categoria não encontrada ou sem permissão de acesso.");
        }

        expense.Update(
            categoryId:    dto.CategoryId,
            sourceType:    dto.SourceType,
            fortnightType: dto.FortnightType,
            description:   dto.Description,
            amount:        dto.Amount,
            dueDate:       dto.DueDate,
            notes:         dto.Notes
        );

        if (dto.Status != expense.PaymentStatus)
        {
            switch (dto.Status)
            {
                case PaymentStatus.Paid:
                    expense.MarkAsPaid(DateOnly.FromDateTime(DateTime.UtcNow));
                    break;
                case PaymentStatus.Cancelled:
                    expense.MarkAsCancelled();
                    break;
                case PaymentStatus.Partial:
                    expense.MarkAsPartial();
                    break;
            }
        }

        await _expenseRepository.UpdateAsync(expense, ct);
        await _unitOfWork.CommitAsync(ct);
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// DELETE EXPENSE
// ══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Realiza soft delete de uma despesa.
/// Verifica posse antes de excluir.
/// </summary>
public sealed class DeleteExpenseUseCase
{
    private readonly IExpenseRepository _expenseRepository;
    private readonly IUnitOfWork        _unitOfWork;

    public DeleteExpenseUseCase(
        IExpenseRepository expenseRepository,
        IUnitOfWork        unitOfWork)
    {
        _expenseRepository = expenseRepository;
        _unitOfWork        = unitOfWork;
    }

    public async Task ExecuteAsync(
        Guid expenseId,
        Guid userId,
        CancellationToken ct = default)
    {
        var expense = await _expenseRepository
            .GetByIdAndUserAsync(expenseId, userId, ct);

        if (expense is null)
            throw new DomainException(
                "Despesa não encontrada ou sem permissão de acesso.");

        expense.SoftDelete();

        await _expenseRepository.UpdateAsync(expense, ct);
        await _unitOfWork.CommitAsync(ct);
    }
}
