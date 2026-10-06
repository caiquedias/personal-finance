using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PersonalFinance.Application.Interfaces;
using PersonalFinance.Domain.Interfaces.Repositories;
using PersonalFinance.Domain.Interfaces.Services;
using PersonalFinance.Infrastructure.Auth;
using PersonalFinance.Infrastructure.Persistence;
using PersonalFinance.Infrastructure.Persistence.Context;
using PersonalFinance.Infrastructure.Persistence.Repositories.Auth;
using PersonalFinance.Infrastructure.Persistence.Repositories.Config;
using PersonalFinance.Infrastructure.Persistence.Repositories.Financial;
using PersonalFinance.Infrastructure.Persistence.Repositories.Reports;
using PersonalFinance.Infrastructure.Services;

namespace PersonalFinance.Infrastructure.Extensions;

public static class InfrastructureExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ── DbContext ─────────────────────────────────────────────────────────
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection"),
                sqlOptions => sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorNumbersToAdd: null)));

        // ── Migrations + view no startup ──────────────────────────────────────
        services.AddHostedService<DatabaseInitializer>();

        // ── Unit of Work ──────────────────────────────────────────────────────
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // ── Repositórios — Domain ─────────────────────────────────────────────
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IPeriodRepository, PeriodRepository>();
        services.AddScoped<IExpenseRepository, ExpenseRepository>();
        services.AddScoped<IExpenseOrderRepository, ExpenseOrderRepository>();
        services.AddScoped<IIncomeRepository, IncomeRepository>();
        services.AddScoped<IReportRepository, ReportRepository>();

        // ── Repositórios — Config / Admin ─────────────────────────────────────
        services.AddScoped<IPaymentStatusRepository, PaymentStatusRepository>();
        services.AddScoped<ISourceTypeRepository, SourceTypeRepository>();
        services.AddScoped<IFortnightTypeRepository, FortnightTypeRepository>();
        services.AddScoped<IUserRoleRepository, UserRoleRepository>();
        services.AddScoped<ILoginThrottleRepository, LoginThrottleRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IAdminUserRepository, AdminUserRepository>();

        // ── Auth ──────────────────────────────────────────────────────────────
        services.Configure<JwtSettings>(configuration.GetSection("JwtSettings"));
        services.AddScoped<IPasswordHasher, Argon2PasswordHasher>();
        services.AddScoped<ITokenService, JwtTokenService>();

        // MFA/TOTP (#393): cifra do secret (AES-256-GCM) e serviço TOTP. As options Auth:Mfa
        // (com validação no startup) são registradas em AddApplicationUseCases.
        services.AddScoped<IMfaRecoveryCodeRepository, MfaRecoveryCodeRepository>();
        services.AddSingleton<ITotpService, TotpService>();
        services.AddSingleton<ISecretProtector, AesGcmSecretProtector>();

        // Reset de senha / verificação de e-mail (#404). As options Auth:UserTokens são registradas na Api (task 11).
        services.AddScoped<IUserTokenRepository, UserTokenRepository>();
        services.AddSingleton<IOneTimeCodeService, HmacOneTimeCodeService>();

        // ── Import (legado Excel) ─────────────────────────────────────────────────────
        services.AddScoped<IExcelParserService, ExcelParserService>();
        services.AddScoped<IStatementParserService, C6StatementPdfParserService>();

        // ── Expurgo ───────────────────────────────────────────────────────────
        services.AddScoped<ICsvExportService, CsvExportService>();
        services.AddScoped<IPurgeRepository, PurgeRepository>();

        return services;
    }
}
