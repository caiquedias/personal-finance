using PersonalFinance.Domain.Exceptions;
using PersonalFinance.Domain.Interfaces.Repositories;

namespace PersonalFinance.Application.UseCases.Financial.Periods;

/// <summary>
/// Ativa ou desativa um período (toggle).
/// Idempotente: chamar duas vezes volta ao estado original.
/// Admin não pode fazer nada aqui — operação é do próprio usuário sobre seus períodos.
/// </summary>
public sealed class TogglePeriodActiveUseCase
{
    private readonly IPeriodRepository _periodRepository;
    private readonly IUnitOfWork       _unitOfWork;

    public TogglePeriodActiveUseCase(
        IPeriodRepository periodRepository,
        IUnitOfWork       unitOfWork)
    {
        _periodRepository = periodRepository;
        _unitOfWork       = unitOfWork;
    }

    public async Task ExecuteAsync(
        Guid periodId,
        Guid userId,
        CancellationToken ct = default)
    {
        var period = await _periodRepository.GetByIdAndUserAsync(periodId, userId, ct)
            ?? throw new DomainException(
                "Período não encontrado ou sem permissão de acesso.");

        if (period.IsActive)
            period.SoftDelete();
        else
            period.Reactivate();

        await _unitOfWork.CommitAsync(ct);
    }
}
