using PersonalFinance.Application.DTOs.Financial;
using PersonalFinance.Domain.Entities.Financial;
using PersonalFinance.Domain.Exceptions;
using PersonalFinance.Domain.Interfaces.Repositories;

namespace PersonalFinance.Application.UseCases.Financial.Expenses
{
    /// <summary>
    /// Retorna todas as despesas de um período específico do usuário,
    /// ordenadas por data de vencimento.
    /// </summary>
    public sealed class GetExpensesByPeriodUseCase
    {
        private readonly IExpenseRepository _expenseRepository;
        private readonly IPeriodRepository _periodRepository;

        public GetExpensesByPeriodUseCase(
            IExpenseRepository expenseRepository,
            IPeriodRepository periodRepository)
        {
            _expenseRepository = expenseRepository;
            _periodRepository = periodRepository;
        }

        public async Task<IEnumerable<ExpenseResponseDto>> ExecuteAsync(
            Guid periodId, Guid userId, CancellationToken ct = default)
        {
            // Garante que o período pertence ao usuário antes de retornar as despesas
            var periodExists = await _periodRepository.ExistsByIdAndUserAsync(periodId, userId, ct);
            if (!periodExists)
                throw new DomainException("Período não encontrado ou sem permissão de acesso.");

            var expenses = await _expenseRepository.GetByPeriodAsync(periodId, userId, ct);
            return expenses.Select(ToDto);
        }

        private static ExpenseResponseDto ToDto(Expense e) =>
            new(e.Id, e.PeriodId, e.UserId, e.CategoryId,
                e.SourceType, e.FortnightType, e.PaymentStatus,
                e.Description, e.Amount, e.DueDate, e.PaymentDate,
                e.Notes, e.IsActive);
    }
}
