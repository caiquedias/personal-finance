using PersonalFinance.Application.DTOs.Financial;
using PersonalFinance.Domain.Interfaces.Repositories;

namespace PersonalFinance.Application.UseCases.Financial.Incomes
{
    /// <summary>
    /// Retorna uma receita específica do usuário.
    /// </summary>
    public sealed class GetIncomeByIdUseCase
    {
        private readonly IIncomeRepository _repository;

        public GetIncomeByIdUseCase(IIncomeRepository repository)
            => _repository = repository;

        public async Task<IncomeResponseDto> ExecuteAsync(
            Guid incomeId, Guid userId, CancellationToken ct = default)
        {
            var income = await _repository.GetByIdAndUserAsync(incomeId, userId, ct)
                ?? throw new KeyNotFoundException("Receita não encontrada.");

            return new IncomeResponseDto(
                income.Id, income.PeriodId, income.UserId, income.FortnightType,
                income.Description, income.Amount, income.ReceivedAt,
                income.Notes, income.IsActive);
        }
    }
}
