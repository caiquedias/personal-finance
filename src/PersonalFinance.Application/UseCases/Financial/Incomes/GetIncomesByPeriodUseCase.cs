using PersonalFinance.Application.DTOs;
using PersonalFinance.Application.DTOs.Financial;
using PersonalFinance.Domain.Entities.Financial;
using PersonalFinance.Domain.Exceptions;
using PersonalFinance.Domain.Interfaces.Repositories;

namespace PersonalFinance.Application.UseCases.Financial.Incomes
{
    /// <summary>
    /// Retorna todas as receitas de um período específico do usuário,
    /// ordenadas por data de recebimento.
    /// </summary>
    public sealed class GetIncomesByPeriodUseCase
    {
        private readonly IIncomeRepository _incomeRepository;
        private readonly IPeriodRepository _periodRepository;

        public GetIncomesByPeriodUseCase(
            IIncomeRepository incomeRepository,
            IPeriodRepository periodRepository)
        {
            _incomeRepository = incomeRepository;
            _periodRepository = periodRepository;
        }

        public async Task<PagedResult<IncomeResponseDto>> ExecuteAsync(
            Guid periodId, Guid userId, IncomeFilterDto filter, CancellationToken ct = default)
        {
            var periodExists = await _periodRepository.ExistsByIdAndUserAsync(periodId, userId, ct);
            if (!periodExists)
                throw new DomainException("Período não encontrado ou sem permissão de acesso.");

            var (items, totalCount) = await _incomeRepository.GetPagedByPeriodAsync(
                periodId, userId,
                filter.PageNumber, filter.PageSize,
                filter.Description, filter.FortnightType,
                ct);

            return new PagedResult<IncomeResponseDto>(
                items.Select(ToDto),
                totalCount,
                filter.PageNumber,
                filter.PageSize);
        }

        private static IncomeResponseDto ToDto(Income i) =>
            new(i.Id, i.PeriodId, i.UserId, i.FortnightType,
                i.Description, i.Amount, i.ReceivedAt, i.Notes, i.IsActive);
    }
}
