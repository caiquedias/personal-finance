using PersonalFinance.Application.DTOs.Import;
using PersonalFinance.Application.Interfaces;
using PersonalFinance.Domain.Entities.Config;
using PersonalFinance.Domain.Entities.Financial;
using PersonalFinance.Domain.Enums;
using PersonalFinance.Domain.Interfaces.Repositories;

namespace PersonalFinance.Application.UseCases.Import;

/// <summary>
/// Orquestra a importação do histórico financeiro legado da planilha Excel.
///
/// Fluxo por aba:
///   1. Garante existência das categorias globais necessárias
///   2. Cria o período mensal se não existir
///   3. Importa receitas primárias (SUM de B4/C4)
///   4. Importa despesas planejadas (tabela A-E)
///   5. Importa gastos diversos e valores recebidos (tabela P/Q por cor)
///
/// Períodos já existentes são ignorados (idempotente por período).
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
        int periodsSkipped = 0;
        int categoriesCreated = 0;
        int expensesImported = 0;
        int incomesImported = 0;
        int expensesSkipped = 0;
        int incomesSkipped = 0;

        // Garante todas as categorias globais necessárias antes de processar
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

        // Categoria padrão para receitas sem categoria específica
        var (receitaDiversaId, receitaCreated) = await EnsureCategoryAsync("Receita Diversa", ct);
        if (receitaCreated) categoriesCreated++;

        await _unitOfWork.CommitAsync(ct);

        // Processa cada aba (período)
        foreach (var sheet in sheets)
        {
            warnings.AddRange(sheet.Warnings);

            // Ignora período já existente — idempotente
            var alreadyExists = await _periodRepository.ExistsAsync(
                userId, sheet.Year, sheet.Month, ct);

            if (alreadyExists)
            {
                periodsSkipped++;
                warnings.Add(
                    $"Período {sheet.Month:D2}/{sheet.Year} já existe — ignorado.");
                continue;
            }

            var period = Period.Create(userId, sheet.Year, sheet.Month);
            await _periodRepository.AddAsync(period, ct);

            // Receitas
            foreach (var income in sheet.Incomes)
            {
                if (income.Amount <= 0)
                {
                    incomesSkipped++;
                    continue;
                }

                var entity = Income.Create(
                    period.Id, userId,
                    income.FortnightType,
                    income.Description,
                    income.Amount,
                    income.ReceivedAt,
                    null);

                await _incomeRepository.AddAsync(entity, ct);
                incomesImported++;
            }

            // Despesas
            foreach (var expense in sheet.Expenses)
            {
                if (expense.Amount <= 0)
                {
                    expensesSkipped++;
                    continue;
                }

                if (!categoryCache.TryGetValue(expense.CategoryName, out var catId))
                {
                    var (newCatId, newCatCreated) = await EnsureCategoryAsync(expense.CategoryName, ct);
                    catId = newCatId;
                    if (newCatCreated) categoriesCreated++;
                    categoryCache[expense.CategoryName] = catId;
                }

                var status = expense.IsPaid
                    ? PaymentStatus.Paid
                    : PaymentStatus.Pending;

                var paymentDate = expense.IsPaid ? expense.DueDate : (DateOnly?)null;

                var entity = Expense.Create(
                    period.Id, userId, catId,
                    expense.SourceType,
                    expense.FortnightType,
                    status,
                    expense.Description,
                    expense.Amount,
                    expense.DueDate,
                    paymentDate,
                    null);

                await _expenseRepository.AddAsync(entity, ct);
                expensesImported++;
            }

            await _unitOfWork.CommitAsync(ct);
            periodsCreated++;
        }

        return new ImportResultDto(
            PeriodsCreated: periodsCreated,
            PeriodsSkipped: periodsSkipped,
            CategoriesCreated: categoriesCreated,
            ExpensesImported: expensesImported,
            IncomesImported: incomesImported,
            ExpensesSkipped: expensesSkipped,
            IncomesSkipped: incomesSkipped,
            Warnings: warnings
        );
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Retorna (id, wasCreated) — wasCreated=true se a categoria foi criada agora.
    /// Async não suporta ref params, por isso o retorno é um tuple.
    /// </summary>
    private async Task<(Guid id, bool wasCreated)> EnsureCategoryAsync(
        string name, CancellationToken ct)
    {
        var existing = await _categoryRepository
            .GetGlobalByNameAsync(name, ct);

        if (existing is not null)
            return (existing.Id, wasCreated: false);

        var color = CategoryColors.GetFor(name);
        var category = Category.CreateGlobal(name, color, null);
        await _categoryRepository.AddAsync(category, ct);
        return (category.Id, wasCreated: true);
    }
}

/// <summary>
/// Cores padrão por categoria — usadas na criação automática durante a importação.
/// </summary>
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
        Map.TryGetValue(name, out var color) ? color : "#8a7a68";
}
