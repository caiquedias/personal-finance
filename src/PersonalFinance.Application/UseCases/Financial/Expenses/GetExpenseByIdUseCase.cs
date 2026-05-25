using PersonalFinance.Application.DTOs.Financial;
using PersonalFinance.Domain.Interfaces.Repositories;

namespace PersonalFinance.Application.UseCases.Financial.Expenses
{
    /// <summary>
    /// Retorna uma despesa específica do usuário.
    /// </summary>
    public sealed class GetExpenseByIdUseCase
    {
        private readonly IExpenseRepository _repository;

        public GetExpenseByIdUseCase(IExpenseRepository repository)
            => _repository = repository;

        public async Task<ExpenseResponseDto> ExecuteAsync(
            Guid expenseId, Guid userId, CancellationToken ct = default)
        {
            var expense = await _repository.GetByIdAndUserAsync(expenseId, userId, ct)
                ?? throw new KeyNotFoundException("Despesa não encontrada.");

            return new ExpenseResponseDto(
                expense.Id, expense.PeriodId, expense.UserId, expense.CategoryId,
                expense.SourceType, expense.FortnightType, expense.PaymentStatus,
                expense.Description, expense.Amount, expense.DueDate, expense.PaymentDate,
                expense.Notes, expense.IsActive, expense.IsRecurring, expense.UpdatedAt);
        }
    }
}
