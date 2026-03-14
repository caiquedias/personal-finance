using PersonalFinance.Application.DTOs.Financial;
using PersonalFinance.Application.Interfaces;
using PersonalFinance.Domain.Entities.Financial;
using PersonalFinance.Domain.Exceptions;
using PersonalFinance.Domain.Interfaces.Repositories;

namespace PersonalFinance.Application.UseCases.Financial.Periods;

// ══════════════════════════════════════════════════════════════════════════════
// CREATE PERIOD
// ══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Cria um novo período mensal para o usuário.
/// A unicidade (UserId + Year + Month) é verificada antes da criação.
/// </summary>
public sealed class CreatePeriodUseCase
{
    private readonly IPeriodRepository _periodRepository;
    private readonly IUnitOfWork       _unitOfWork;

    public CreatePeriodUseCase(
        IPeriodRepository periodRepository,
        IUnitOfWork       unitOfWork)
    {
        _periodRepository = periodRepository;
        _unitOfWork       = unitOfWork;
    }

    public async Task<PeriodResponseDto> ExecuteAsync(
        CreatePeriodDto dto,
        CancellationToken ct = default)
    {
        var exists = await _periodRepository
            .ExistsAsync(dto.UserId, dto.Year, dto.Month, ct);

        if (exists)
            throw new DomainException(
                $"Já existe um período para {dto.Month:D2}/{dto.Year}.");

        var period = Period.Create(dto.UserId, dto.Year, dto.Month);

        await _periodRepository.AddAsync(period, ct);
        await _unitOfWork.CommitAsync(ct);

        return ToDto(period);
    }

    private static PeriodResponseDto ToDto(Period p) =>
        new(p.Id, p.UserId, p.Year, p.Month, p.IsActive);
}

// ══════════════════════════════════════════════════════════════════════════════
// GET PERIOD SUMMARY
// ══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Retorna o resumo financeiro de um período via vw_PeriodSummary.
/// Carregado por demanda — não mantido em tabela física.
/// </summary>
public sealed class GetPeriodSummaryUseCase
{
    private readonly IPeriodRepository _periodRepository;
    private readonly IReportRepository _reportRepository;

    public GetPeriodSummaryUseCase(
        IPeriodRepository periodRepository,
        IReportRepository reportRepository)
    {
        _periodRepository = periodRepository;
        _reportRepository = reportRepository;
    }

    public async Task<PeriodSummaryDto> ExecuteAsync(
        Guid periodId,
        Guid userId,
        CancellationToken ct = default)
    {
        // Verifica posse do período antes de carregar o resumo
        var periodExists = await _periodRepository
            .ExistsByIdAndUserAsync(periodId, userId, ct);

        if (!periodExists)
            throw new DomainException(
                "Período não encontrado ou sem permissão de acesso.");

        var summary = await _reportRepository
            .GetPeriodSummaryAsync(periodId, userId, ct);

        // Fallback: período existe mas ainda não tem lançamentos
        return summary ?? new PeriodSummaryDto(
            PeriodId:             periodId,
            UserId:               userId,
            Year:                 0,
            Month:                0,
            TotalIncome:          0,
            TotalExpense:         0,
            TotalPaid:            0,
            TotalOwed:            0,
            TotalFirstFortnight:  0,
            TotalSecondFortnight: 0,
            Balance:              0
        );
    }
}
