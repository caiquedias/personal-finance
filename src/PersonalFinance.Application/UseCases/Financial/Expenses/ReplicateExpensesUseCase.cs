using PersonalFinance.Application.DTOs.Financial;
using PersonalFinance.Domain.Entities.Financial;
using PersonalFinance.Domain.Enums;
using PersonalFinance.Domain.Exceptions;
using PersonalFinance.Domain.Interfaces.Repositories;

namespace PersonalFinance.Application.UseCases.Financial.Expenses;

/// <summary>
/// Replica despesas recorrentes selecionadas para um período destino.
/// - DueDate ajustado para o mês/ano do período destino, mantendo o dia original
/// - PaymentStatus = Pending, PaymentDate = null
/// - Idempotente: ignora despesas já replicadas no período destino (via SourceExpenseId)
/// </summary>
public sealed class ReplicateExpensesUseCase
{
    private readonly IExpenseRepository _expenseRepository;
    private readonly IPeriodRepository  _periodRepository;
    private readonly IUnitOfWork        _unitOfWork;

    public ReplicateExpensesUseCase(
        IExpenseRepository expenseRepository,
        IPeriodRepository  periodRepository,
        IUnitOfWork        unitOfWork)
    {
        _expenseRepository = expenseRepository;
        _periodRepository  = periodRepository;
        _unitOfWork        = unitOfWork;
    }

    public async Task ExecuteAsync(ReplicateExpensesDto dto, CancellationToken ct = default)
    {
        if (dto.ExpenseIds.Count == 0)
            throw new DomainException("A lista de despesas não pode ser vazia.");

        var period = await _periodRepository.GetByIdAndUserAsync(dto.TargetPeriodId, dto.UserId, ct)
            ?? throw new DomainException("Período destino não encontrado ou sem permissão de acesso.");

        var sourceExpenses = (await _expenseRepository
            .GetByIdsAndUserAsync(dto.ExpenseIds, dto.UserId, ct)).ToList();

        if (sourceExpenses.Count == 0)
            throw new DomainException("Nenhuma despesa encontrada ou sem permissão de acesso.");

        var toAdd = new List<Expense>();

        foreach (var source in sourceExpenses)
        {
            // Idempotência: pula se já replicada neste período
            var alreadyReplicated = await _expenseRepository
                .HasReplicatedExpenseAsync(source.Id, dto.TargetPeriodId, ct);

            if (alreadyReplicated)
                continue;

            // Ajusta DueDate para mês/ano do período destino, mantendo o dia original
            var day = Math.Min(source.DueDate.Day,
                DateTime.DaysInMonth(period.Year, period.Month));
            var dueDate = new DateOnly(period.Year, period.Month, day);

            var replicated = Expense.Create(
                periodId:        dto.TargetPeriodId,
                userId:          dto.UserId,
                categoryId:      source.CategoryId,
                sourceType:      source.SourceType,
                fortnightType:   source.FortnightType,
                paymentStatus:   PaymentStatus.Pending,
                description:     source.Description,
                amount:          source.Amount,
                dueDate:         dueDate,
                paymentDate:     null,
                notes:           source.Notes,
                isRecurring:     source.IsRecurring,
                sourceExpenseId: source.Id
            );

            toAdd.Add(replicated);
        }

        if (toAdd.Count > 0)
        {
            await _expenseRepository.AddRangeAsync(toAdd, ct);
            await _unitOfWork.CommitAsync(ct);
        }
    }
}
