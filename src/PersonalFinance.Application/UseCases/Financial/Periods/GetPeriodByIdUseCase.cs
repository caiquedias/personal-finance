using PersonalFinance.Application.DTOs.Financial;
using PersonalFinance.Domain.Interfaces.Repositories;

namespace PersonalFinance.Application.UseCases.Financial.Periods
{
    /// <summary>
    /// Retorna um período específico do usuário.
    /// </summary>
    public sealed class GetPeriodByIdUseCase
    {
        private readonly IPeriodRepository _repository;

        public GetPeriodByIdUseCase(IPeriodRepository repository)
            => _repository = repository;

        public async Task<PeriodResponseDto> ExecuteAsync(
            Guid periodId, Guid userId, CancellationToken ct = default)
        {
            var period = await _repository.GetByIdAndUserAsync(periodId, userId, ct)
                ?? throw new KeyNotFoundException("Período não encontrado.");

            return new PeriodResponseDto(period.Id, period.UserId, period.Year, period.Month, period.IsActive);
        }
    }
}
