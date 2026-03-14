using PersonalFinance.Application.DTOs.Financial;
using PersonalFinance.Domain.Interfaces.Repositories;

namespace PersonalFinance.Application.UseCases.Financial.Periods
{
    /// <summary>
    /// Retorna todos os períodos do usuário, ordenados por ano/mês descendente.
    /// </summary>
    public sealed class GetPeriodsByUserUseCase
    {
        private readonly IPeriodRepository _repository;

        public GetPeriodsByUserUseCase(IPeriodRepository repository)
            => _repository = repository;

        public async Task<IEnumerable<PeriodResponseDto>> ExecuteAsync(
            Guid userId, CancellationToken ct = default)
        {
            var periods = await _repository.GetByUserAsync(userId, ct);
            return periods.Select(p => new PeriodResponseDto(p.Id, p.UserId, p.Year, p.Month, p.IsActive));
        }
    }
}
