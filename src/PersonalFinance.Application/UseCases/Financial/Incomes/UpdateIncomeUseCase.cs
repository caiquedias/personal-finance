using FluentValidation;
using PersonalFinance.Application.DTOs.Financial;
using PersonalFinance.Domain.Exceptions;
using PersonalFinance.Domain.Interfaces.Repositories;

namespace PersonalFinance.Application.UseCases.Financial.Incomes;

/// <summary>
/// Atualiza os dados editáveis de uma receita existente.
/// Verifica posse antes de atualizar.
/// </summary>
public sealed class UpdateIncomeUseCase
{
    private readonly IIncomeRepository _incomeRepository;
    private readonly IUnitOfWork       _unitOfWork;
    private readonly IValidator<UpdateIncomeDto> _validator;

    public UpdateIncomeUseCase(
        IIncomeRepository incomeRepository,
        IUnitOfWork       unitOfWork,
        IValidator<UpdateIncomeDto> validator)
    {
        _incomeRepository = incomeRepository;
        _unitOfWork       = unitOfWork;
        _validator        = validator;
    }

    public async Task ExecuteAsync(
        UpdateIncomeDto dto,
        CancellationToken ct = default)
    {
        await _validator.ValidateAndThrowAsync(dto, ct);

        var income = await _incomeRepository
            .GetByIdAndUserAsync(dto.Id, dto.UserId, ct);

        if (income is null)
            throw new DomainException(
                "Receita não encontrada ou sem permissão de acesso.");

        income.Update(
            fortnightType: dto.FortnightType,
            description:   dto.Description,
            amount:        dto.Amount,
            receivedAt:    dto.ReceivedAt,
            notes:         dto.Notes
        );

        await _incomeRepository.UpdateAsync(income, ct);
        await _unitOfWork.CommitAsync(ct);
    }
}
