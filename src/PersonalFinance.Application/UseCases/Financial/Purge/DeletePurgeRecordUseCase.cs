using PersonalFinance.Domain.Exceptions;
using PersonalFinance.Domain.Interfaces.Repositories;

namespace PersonalFinance.Application.UseCases.Financial.Purge;

/// <summary>
/// Remove um registro de expurgo pelo Id, validando o ownership do usuário.
/// </summary>
public sealed class DeletePurgeRecordUseCase
{
    private readonly IPurgeRepository _purgeRepository;
    private readonly IUnitOfWork      _unitOfWork;

    public DeletePurgeRecordUseCase(
        IPurgeRepository purgeRepository,
        IUnitOfWork      unitOfWork)
    {
        _purgeRepository = purgeRepository;
        _unitOfWork      = unitOfWork;
    }

    public async Task ExecuteAsync(
        Guid id,
        Guid userId,
        CancellationToken ct = default)
    {
        var record = await _purgeRepository.GetByIdAndUserAsync(id, userId, ct)
            ?? throw new DomainException(
                "Registro de expurgo não encontrado ou sem permissão de acesso.");

        await _purgeRepository.DeleteAsync(record, ct);
        await _unitOfWork.CommitAsync(ct);
    }
}
