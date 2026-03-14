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

namespace PersonalFinance.Infrastructure.Extensions;

public static class InfrastructureExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration          configuration)
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
        services.AddScoped<IUserRepository,     UserRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IPeriodRepository,   PeriodRepository>();
        services.AddScoped<IExpenseRepository,  ExpenseRepository>();
        services.AddScoped<IIncomeRepository,   IncomeRepository>();
        services.AddScoped<IReportRepository,   ReportRepository>();

        // ── Repositórios — Config / Admin ─────────────────────────────────────
        services.AddScoped<IPaymentStatusRepository,  PaymentStatusRepository>();
        services.AddScoped<ISourceTypeRepository,     SourceTypeRepository>();
        services.AddScoped<IFortnightTypeRepository,  FortnightTypeRepository>();
        services.AddScoped<IUserRoleRepository,       UserRoleRepository>();
        services.AddScoped<IAdminUserRepository,      AdminUserRepository>();

        // ── Auth ──────────────────────────────────────────────────────────────
        services.Configure<JwtSettings>(configuration.GetSection("JwtSettings"));
        services.AddScoped<IPasswordHasher, Argon2PasswordHasher>();
        services.AddScoped<ITokenService,   JwtTokenService>();

        return services;
    }
}
