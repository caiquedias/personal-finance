using PersonalFinance.Application.DTOs.Financial;
using PersonalFinance.Domain.Entities.Financial;
using PersonalFinance.Domain.Exceptions;
using PersonalFinance.Domain.Interfaces.Repositories;

namespace PersonalFinance.Application.UseCases.Financial.Incomes;

// ══════════════════════════════════════════════════════════════════════════════
// CREATE INCOME
// ══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Registra uma nova receita em um período existente.
/// Valida posse do período antes de criar.
/// </summary>
public sealed class CreateIncomeUseCase
{
    private readonly IIncomeRepository _incomeRepository;
    private readonly IPeriodRepository _periodRepository;
    private readonly IUnitOfWork       _unitOfWork;

    public CreateIncomeUseCase(
        IIncomeRepository incomeRepository,
        IPeriodRepository periodRepository,
        IUnitOfWork       unitOfWork)
    {
        _incomeRepository = incomeRepository;
        _periodRepository = periodRepository;
        _unitOfWork       = unitOfWork;
    }

    public async Task<IncomeResponseDto> ExecuteAsync(
        CreateIncomeDto dto,
        CancellationToken ct = default)
    {
        var periodExists = await _periodRepository
            .ExistsByIdAndUserAsync(dto.PeriodId, dto.UserId, ct);

        if (!periodExists)
            throw new DomainException(
                "Período não encontrado ou sem permissão de acesso.");

        var income = Income.Create(
            periodId:      dto.PeriodId,
            userId:        dto.UserId,
            fortnightType: dto.FortnightType,
            description:   dto.Description,
            amount:        dto.Amount,
            receivedAt:    dto.ReceivedAt,
            notes:         dto.Notes
        );

        await _incomeRepository.AddAsync(income, ct);
        await _unitOfWork.CommitAsync(ct);

        return ToDto(income);
    }

    private static IncomeResponseDto ToDto(Income i) =>
        new(i.Id, i.PeriodId, i.UserId, i.FortnightType,
            i.Description, i.Amount, i.ReceivedAt, i.Notes, i.IsActive);
}

// ══════════════════════════════════════════════════════════════════════════════
// DELETE INCOME
// ══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Realiza soft delete de uma receita.
/// Verifica posse antes de excluir.
/// </summary>
public sealed class DeleteIncomeUseCase
{
    private readonly IIncomeRepository _incomeRepository;
    private readonly IUnitOfWork       _unitOfWork;

    public DeleteIncomeUseCase(
        IIncomeRepository incomeRepository,
        IUnitOfWork       unitOfWork)
    {
        _incomeRepository = incomeRepository;
        _unitOfWork       = unitOfWork;
    }

    public async Task ExecuteAsync(
        Guid incomeId,
        Guid userId,
        CancellationToken ct = default)
    {
        var income = await _incomeRepository
            .GetByIdAndUserAsync(incomeId, userId, ct);

        if (income is null)
            throw new DomainException(
                "Receita não encontrada ou sem permissão de acesso.");

        income.SoftDelete();

        await _incomeRepository.UpdateAsync(income, ct);
        await _unitOfWork.CommitAsync(ct);
    }
}
