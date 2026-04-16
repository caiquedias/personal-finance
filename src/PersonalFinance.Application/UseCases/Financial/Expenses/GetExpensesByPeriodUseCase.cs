using PersonalFinance.Application.DTOs;
using PersonalFinance.Application.DTOs.Financial;
using PersonalFinance.Domain.Entities.Financial;
using PersonalFinance.Domain.Exceptions;
using PersonalFinance.Domain.Interfaces.Repositories;

namespace PersonalFinance.Application.UseCases.Financial.Expenses
{
    /// <summary>
    /// Retorna as despesas de um período de forma paginada com filtros opcionais.
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

        public async Task<PagedResult<ExpenseResponseDto>> ExecuteAsync(
            Guid periodId, Guid userId, ExpenseFilterDto filter, CancellationToken ct = default)
        {
            // Garante que o período pertence ao usuário antes de retornar as despesas
            var periodExists = await _periodRepository.ExistsByIdAndUserAsync(periodId, userId, ct);
            if (!periodExists)
                throw new DomainException("Período não encontrado ou sem permissão de acesso.");

            var (items, totalCount) = await _expenseRepository.GetPagedByPeriodAsync(
                periodId, userId,
                filter.PageNumber, filter.PageSize,
                filter.Description, filter.CategoryId,
                filter.PaymentStatus, filter.FortnightType,
                filter.SourceType,
                ct);

            return new PagedResult<ExpenseResponseDto>(
                items.Select(ToDto),
                totalCount,
                filter.PageNumber,
                filter.PageSize);
        }

        private static ExpenseResponseDto ToDto(Expense e) =>
            new(e.Id, e.PeriodId, e.UserId, e.CategoryId,
                e.SourceType, e.FortnightType, e.PaymentStatus,
                e.Description, e.Amount, e.DueDate, e.PaymentDate,
                e.Notes, e.IsActive);
    }
}
