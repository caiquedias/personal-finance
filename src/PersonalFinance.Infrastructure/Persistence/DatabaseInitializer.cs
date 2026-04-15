using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PersonalFinance.Infrastructure.Persistence.Context;

namespace PersonalFinance.Infrastructure.Persistence;

/// <summary>
/// Serviço que executa as migrations pendentes e garante que a vw_PeriodSummary
/// existe ao iniciar a aplicação.
/// Registrado como IHostedService — executado antes de aceitar requisições.
/// </summary>
public sealed class DatabaseInitializer : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(
        IServiceScopeFactory scopeFactory,
        ILogger<DatabaseInitializer> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        _logger.LogInformation("Iniciando DatabaseInitializer...");

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        try
        {
            // Aplica migrations pendentes (inclui seed do HasData)
            await context.Database.MigrateAsync(ct);
            _logger.LogInformation("Migrations aplicadas com sucesso.");

            // Garante que a view existe (idempotente via CREATE OR ALTER)
            await EnsurePeriodSummaryViewAsync(context, ct);
            _logger.LogInformation("vw_PeriodSummary verificada.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao inicializar o banco de dados.");
            throw;
        }
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;

    // ── View ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Cria ou atualiza a vw_PeriodSummary.
    /// CREATE OR ALTER é idempotente — seguro executar em toda inicialização.
    /// </summary>
    private static async Task EnsurePeriodSummaryViewAsync(
        AppDbContext context, CancellationToken ct)
    {
        const string sql = """
            CREATE OR ALTER VIEW [dbo].[vw_PeriodSummary] AS
            SELECT
                p.[Id]                                                         AS [PeriodId],
                p.[UserId],
                p.[Year],
                p.[Month],

                -- Receitas em subconsulta separada — evita produto cartesiano com Expense
                COALESCE(i.[TotalIncome], 0)                                   AS [TotalIncome],

                -- Despesas
                COALESCE(e.[TotalExpense],         0)                          AS [TotalExpense],
                COALESCE(e.[TotalPaid],            0)                          AS [TotalPaid],
                COALESCE(e.[TotalOwed],            0)                          AS [TotalOwed],
                COALESCE(e.[TotalFirstFortnight],  0)                          AS [TotalFirstFortnight],
                COALESCE(e.[TotalSecondFortnight], 0)                          AS [TotalSecondFortnight],

                -- Saldo
                COALESCE(i.[TotalIncome], 0) - COALESCE(e.[TotalPaid], 0)  AS [Balance]

            FROM [dbo].[Period] p

            LEFT JOIN (
                SELECT [PeriodId], SUM([Amount]) AS [TotalIncome]
                FROM [dbo].[Income]
                WHERE [DeletedAt] IS NULL
                GROUP BY [PeriodId]
            ) i ON i.[PeriodId] = p.[Id]

            LEFT JOIN (
                SELECT
                    [PeriodId],
                    SUM([Amount])                                                        AS [TotalExpense],
                    SUM(CASE WHEN [PaymentStatusId] = 2       THEN [Amount] ELSE 0 END) AS [TotalPaid],
                    SUM(CASE WHEN [PaymentStatusId] IN (1, 4) THEN [Amount] ELSE 0 END) AS [TotalOwed],
                    SUM(CASE WHEN [FortnightTypeId] = 1       THEN [Amount] ELSE 0 END) AS [TotalFirstFortnight],
                    SUM(CASE WHEN [FortnightTypeId] = 2       THEN [Amount] ELSE 0 END) AS [TotalSecondFortnight]
                FROM [dbo].[Expense]
                WHERE [DeletedAt] IS NULL
                GROUP BY [PeriodId]
            ) e ON e.[PeriodId] = p.[Id]

            WHERE p.[DeletedAt] IS NULL;
            """;

        await context.Database.ExecuteSqlRawAsync(sql, ct);
    }
}
