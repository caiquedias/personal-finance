using PersonalFinance.Domain.Exceptions;
using PersonalFinance.Domain.Interfaces.Repositories;

namespace PersonalFinance.Application.UseCases.Financial.Periods;

/// <summary>
/// Realiza soft delete de um período.
/// Bloqueia a exclusão se o período tiver despesas ou receitas vinculadas,
/// pois a exclusão deixaria lançamentos órfãos sem período de referência.
/// </summary>
public sealed class DeletePeriodUseCase
{
    private readonly IPeriodRepository  _periodRepository;
    private readonly IExpenseRepository _expenseRepository;
    private readonly IIncomeRepository  _incomeRepository;
    private readonly IUnitOfWork        _unitOfWork;

    public DeletePeriodUseCase(
        IPeriodRepository  periodRepository,
        IExpenseRepository expenseRepository,
        IIncomeRepository  incomeRepository,
        IUnitOfWork        unitOfWork)
    {
        _periodRepository  = periodRepository;
        _expenseRepository = expenseRepository;
        _incomeRepository  = incomeRepository;
        _unitOfWork        = unitOfWork;
    }

    public async Task ExecuteAsync(
        Guid periodId,
        Guid userId,
        CancellationToken ct = default)
    {
        var period = await _periodRepository.GetByIdAndUserAsync(periodId, userId, ct)
            ?? throw new DomainException(
                "Período não encontrado ou sem permissão de acesso.");

        // Impede exclusão de períodos com lançamentos — integridade referencial no domínio
        var expenses = await _expenseRepository.GetByPeriodAsync(periodId, userId, ct);
        if (expenses.Any())
            throw new DomainException(
                "Não é possível excluir o período pois existem despesas vinculadas a ele. " +
                "Exclua as despesas antes de remover o período.");

        var incomes = await _incomeRepository.GetByPeriodAsync(periodId, userId, ct);
        if (incomes.Any())
            throw new DomainException(
                "Não é possível excluir o período pois existem receitas vinculadas a ele. " +
                "Exclua as receitas antes de remover o período.");

        period.SoftDelete();
        await _unitOfWork.CommitAsync(ct);
    }
}
