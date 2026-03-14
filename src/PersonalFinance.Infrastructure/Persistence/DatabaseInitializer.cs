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
                p.[Id]                                                     AS [PeriodId],
                p.[UserId],
                p.[Year],
                p.[Month],

                -- Receitas
                COALESCE(SUM(i.[Amount]), 0)                               AS [TotalIncome],

                -- Despesas
                COALESCE(SUM(e.[Amount]), 0)                               AS [TotalExpense],

                -- Pago (PaymentStatusId = 2)
                COALESCE(SUM(CASE WHEN e.[PaymentStatusId] = 2
                                  THEN e.[Amount] ELSE 0 END), 0)          AS [TotalPaid],

                -- Devedor (Pending=1 ou Partial=4)
                COALESCE(SUM(CASE WHEN e.[PaymentStatusId] IN (1, 4)
                                  THEN e.[Amount] ELSE 0 END), 0)          AS [TotalOwed],

                -- Por quinzena (FortnightTypeId: First=1, Second=2)
                COALESCE(SUM(CASE WHEN e.[FortnightTypeId] = 1
                                  THEN e.[Amount] ELSE 0 END), 0)          AS [TotalFirstFortnight],
                COALESCE(SUM(CASE WHEN e.[FortnightTypeId] = 2
                                  THEN e.[Amount] ELSE 0 END), 0)          AS [TotalSecondFortnight],

                -- Saldo
                COALESCE(SUM(i.[Amount]), 0)
                    - COALESCE(SUM(e.[Amount]), 0)                         AS [Balance]

            FROM      [dbo].[Period]  p
            LEFT JOIN [dbo].[Income]  i ON i.[PeriodId] = p.[Id]
                                       AND i.[DeletedAt] IS NULL
            LEFT JOIN [dbo].[Expense] e ON e.[PeriodId] = p.[Id]
                                       AND e.[DeletedAt] IS NULL
            WHERE     p.[DeletedAt] IS NULL
            GROUP BY  p.[Id], p.[UserId], p.[Year], p.[Month];
            """;

        await context.Database.ExecuteSqlRawAsync(sql, ct);
    }
}
