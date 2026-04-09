using PersonalFinance.Application.DTOs.Import;
using PersonalFinance.Application.Interfaces;
using PersonalFinance.Domain.Entities.Config;
using PersonalFinance.Domain.Entities.Financial;
using PersonalFinance.Domain.Enums;
using PersonalFinance.Domain.Interfaces.Repositories;

namespace PersonalFinance.Application.UseCases.Import;

/// <summary>
/// Importa o histórico financeiro legado da planilha Excel com suporte a re-importação.
///
/// Estratégia de idempotência por entidade:
///
/// PERÍODO:
///   - Não existe → cria
///   - Já existe  → reutiliza (nunca duplica)
///
/// DESPESA (chave: PeriodId + Descrição + Valor + Quinzena):
///   - Não existe → insere
///   - Existe + dados iguais → ignora (sem UPDATE desnecessário)
///   - Existe + dados diferentes (ex: status Pago mudou) → atualiza
///
/// RECEITA (mesma chave):
///   - Não existe → insere
///   - Existe + dados iguais → ignora
///   - Existe + dados diferentes → atualiza
///
/// Registros que existem no banco mas não estão na planilha são preservados.
/// </summary>
public sealed class ImportLegacyDataUseCase
{
    private readonly IExcelParserService _parser;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IPeriodRepository _periodRepository;
    private readonly IExpenseRepository _expenseRepository;
    private readonly IIncomeRepository _incomeRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ImportLegacyDataUseCase(
        IExcelParserService parser,
        ICategoryRepository categoryRepository,
        IPeriodRepository periodRepository,
        IExpenseRepository expenseRepository,
        IIncomeRepository incomeRepository,
        IUnitOfWork unitOfWork)
    {
        _parser = parser;
        _categoryRepository = categoryRepository;
        _periodRepository = periodRepository;
        _expenseRepository = expenseRepository;
        _incomeRepository = incomeRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ImportResultDto> ExecuteAsync(
        Stream fileStream,
        Guid userId,
        CancellationToken ct = default)
    {
        var sheets = await _parser.ParseAsync(fileStream, ct);

        var warnings = new List<string>();
        int periodsCreated = 0;
        int periodsReused = 0;
        int categoriesCreated = 0;
        int expensesInserted = 0;
        int expensesUpdated = 0;
        int expensesUnchanged = 0;
        int incomesInserted = 0;
        int incomesUpdated = 0;
        int incomesUnchanged = 0;
        int expensesSkipped = 0;
        int incomesSkipped = 0;

        // Garante categorias globais antes de processar qualquer aba
        var categoryNames = sheets
            .SelectMany(s => s.Expenses.Select(e => e.CategoryName))
            .Distinct();

        var categoryCache = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

        foreach (var name in categoryNames)
        {
            var (id, wasCreated) = await EnsureCategoryAsync(name, ct);
            categoryCache[name] = id;
            if (wasCreated) categoriesCreated++;
        }

        var (receitaDiversaId, receitaCreated) = await EnsureCategoryAsync("Receita Diversa", ct);
        if (receitaCreated) categoriesCreated++;

        await _unitOfWork.CommitAsync(ct);

        // Processa cada aba
        foreach (var sheet in sheets)
        {
            warnings.AddRange(sheet.Warnings);

            // Garante que o período existe — cria ou reutiliza
            var period = await _periodRepository.GetByUserYearMonthAsync(
                userId, sheet.Year, sheet.Month, ct);

            if (period is null)
            {
                period = Period.Create(userId, sheet.Year, sheet.Month);
                await _periodRepository.AddAsync(period, ct);
                periodsCreated++;
            }
            else
            {
                periodsReused++;
            }

            // ── Upsert de receitas ─────────────────────────────────────────
            foreach (var dto in sheet.Incomes)
            {
                if (dto.Amount <= 0) { incomesSkipped++; continue; }

                var existing = await _incomeRepository.FindByImportKeyAsync(
                    period.Id, dto.Description, dto.Amount, dto.FortnightType, ct);

                if (existing is null)
                {
                    var income = Income.Create(
                        period.Id, userId,
                        dto.FortnightType,
                        dto.Description,
                        dto.Amount,
                        dto.ReceivedAt,
                        null);

                    await _incomeRepository.AddAsync(income, ct);
                    incomesInserted++;
                }
                else if (HasIncomeChanged(existing, dto))
                {
                    existing.Update(
                        dto.FortnightType,
                        dto.Description,
                        dto.Amount,
                        dto.ReceivedAt,
                        existing.Notes);

                    await _incomeRepository.UpdateAsync(existing, ct);
                    incomesUpdated++;
                }
                else
                {
                    incomesUnchanged++;
                }
            }

            // ── Upsert de despesas ─────────────────────────────────────────
            foreach (var dto in sheet.Expenses)
            {
                if (dto.Amount <= 0) { expensesSkipped++; continue; }

                if (!categoryCache.TryGetValue(dto.CategoryName, out var catId))
                {
                    var (newId, wasCreated) = await EnsureCategoryAsync(dto.CategoryName, ct);
                    catId = newId;
                    categoryCache[dto.CategoryName] = catId;
                    if (wasCreated) categoriesCreated++;
                }

                var existing = await _expenseRepository.FindByImportKeyAsync(
                    period.Id, dto.Description, dto.Amount, dto.FortnightType, ct);

                var newStatus = dto.IsPaid ? PaymentStatus.Paid : PaymentStatus.Pending;
                var newPaymentDate = dto.IsPaid ? dto.DueDate : (DateOnly?)null;

                if (existing is null)
                {
                    var expense = Expense.Create(
                        period.Id, userId, catId,
                        dto.SourceType,
                        dto.FortnightType,
                        newStatus,
                        dto.Description,
                        dto.Amount,
                        dto.DueDate,
                        newPaymentDate,
                        null);

                    await _expenseRepository.AddAsync(expense, ct);
                    expensesInserted++;
                }
                else if (HasExpenseChanged(existing, dto, newStatus))
                {
                    // Atualiza apenas campos que podem mudar na planilha
                    // Atualiza campos via Update (sem PaymentStatus — gerenciado por métodos próprios)
                    existing.Update(
                        catId,
                        dto.SourceType,
                        dto.FortnightType,
                        dto.Description,
                        dto.Amount,
                        dto.DueDate,
                        existing.Notes);

                    // Atualiza para Paid se a planilha marcou como pago e o banco ainda não tem
                    // Não reverte de Paid para Pending — decisão do usuário no sistema prevalece
                    if (newStatus == PaymentStatus.Paid && existing.PaymentStatus != PaymentStatus.Paid)
                        existing.MarkAsPaid(dto.DueDate);

                    await _expenseRepository.UpdateAsync(existing, ct);
                    expensesUpdated++;
                }
                else
                {
                    expensesUnchanged++;
                }
            }

            await _unitOfWork.CommitAsync(ct);
        }

        return new ImportResultDto(
            PeriodsCreated: periodsCreated,
            PeriodsSkipped: periodsReused,
            CategoriesCreated: categoriesCreated,
            ExpensesImported: expensesInserted + expensesUpdated,
            IncomesImported: incomesInserted + incomesUpdated,
            ExpensesSkipped: expensesSkipped,
            IncomesSkipped: incomesSkipped,
            Warnings: warnings
        );
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static bool HasIncomeChanged(Income existing, ParsedIncomeDto dto) =>
        existing.FortnightType != dto.FortnightType ||
        existing.ReceivedAt != dto.ReceivedAt;

    private static bool HasExpenseChanged(
        Expense existing, ParsedExpenseDto dto, PaymentStatus newStatus) =>
        // Só considera mudança de Pending→Paid, nunca Paid→Pending
        (newStatus == PaymentStatus.Paid && existing.PaymentStatus != PaymentStatus.Paid) ||
        existing.SourceType != dto.SourceType ||
        existing.FortnightType != dto.FortnightType ||
        existing.DueDate != dto.DueDate;

    private async Task<(Guid id, bool wasCreated)> EnsureCategoryAsync(
        string name, CancellationToken ct)
    {
        var existing = await _categoryRepository.GetGlobalByNameAsync(name, ct);
        if (existing is not null) return (existing.Id, false);

        var color = CategoryColors.GetFor(name);
        var category = Category.CreateGlobal(name, color, null);
        await _categoryRepository.AddAsync(category, ct);
        return (category.Id, true);
    }
}

file static class CategoryColors
{
    private static readonly Dictionary<string, string> Map =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Gasto Recorrente"] = "#527a58",
            ["Gastos com mercado meus pais"] = "#00bcd4",
            ["Gastos com mercado meu Apê"] = "#3f51b5",
            ["Gasto fútil"] = "#f9a825",
            ["Serviços"] = "#ef6c00",
            ["Depósitos de PF (declarar)"] = "#b71c1c",
            ["Investimento + Rendimento"] = "#ad1457",
            ["Despesa Gatinhos"] = "#558b2f",
            ["Gasolina"] = "#7b1fa2",
            ["Uber"] = "#212121",
            ["Receita Diversa"] = "#6b8f71",
        };

    public static string GetFor(string name) =>
        Map.TryGetValue(name, out var c) ? c : "#8a7a68";
}
