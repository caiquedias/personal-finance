using PersonalFinance.Application.UseCases.Admin;
using PersonalFinance.Application.UseCases.Auth;
using PersonalFinance.Application.UseCases.Config;
using PersonalFinance.Application.UseCases.Financial.Expenses;
using PersonalFinance.Application.UseCases.Financial.Incomes;
using PersonalFinance.Application.UseCases.Financial.Periods;
using PersonalFinance.Application.UseCases.Import;

namespace PersonalFinance.Api.Extensions;

public static class ApplicationExtensions
{
    public static IServiceCollection AddApplicationUseCases(
        this IServiceCollection services)
    {
        // ── Auth ──────────────────────────────────────────────────────────────
        services.AddScoped<RegisterUserUseCase>();
        services.AddScoped<LoginWithRolesUseCase>(); // substitui LoginUseCase

        // ── Config — Categories ───────────────────────────────────────────────
        services.AddScoped<GetCategoriesUseCase>();
        services.AddScoped<GetCategoryByIdUseCase>();
        services.AddScoped<CreateCategoryUseCase>();
        services.AddScoped<UpdateCategoryUseCase>();
        services.AddScoped<DeleteCategoryUseCase>();

        // ── Config — Lookup tables ────────────────────────────────────────────
        services.AddScoped<GetPaymentStatusesUseCase>();
        services.AddScoped<CreatePaymentStatusUseCase>();
        services.AddScoped<UpdatePaymentStatusUseCase>();
        services.AddScoped<DeletePaymentStatusUseCase>();
        services.AddScoped<GetSourceTypesUseCase>();
        services.AddScoped<CreateSourceTypeUseCase>();
        services.AddScoped<UpdateSourceTypeUseCase>();
        services.AddScoped<DeleteSourceTypeUseCase>();
        services.AddScoped<GetFortnightTypesUseCase>();
        services.AddScoped<CreateFortnightTypeUseCase>();
        services.AddScoped<UpdateFortnightTypeUseCase>();
        services.AddScoped<DeleteFortnightTypeUseCase>();

        // ── Admin — User management ───────────────────────────────────────────
        services.AddScoped<GetUsersUseCase>();
        services.AddScoped<ToggleUserActiveUseCase>();
        services.AddScoped<AssignRoleUseCase>();
        services.AddScoped<RemoveRoleUseCase>();
        services.AddScoped<ResetUserPasswordUseCase>();

        // ── Financial — Periods ───────────────────────────────────────────────
        services.AddScoped<GetPeriodsByUserUseCase>();
        services.AddScoped<GetPeriodByIdUseCase>();
        services.AddScoped<CreatePeriodUseCase>();
        services.AddScoped<GetPeriodSummaryUseCase>();
        services.AddScoped<TogglePeriodActiveUseCase>();
        services.AddScoped<DeletePeriodUseCase>();

        // ── Financial — Expenses ──────────────────────────────────────────────
        services.AddScoped<GetExpensesByPeriodUseCase>();
        services.AddScoped<GetExpenseByIdUseCase>();
        services.AddScoped<CreateExpenseUseCase>();
        services.AddScoped<UpdateExpenseUseCase>();
        services.AddScoped<DeleteExpenseUseCase>();

        // ── Financial — Incomes ───────────────────────────────────────────────
        services.AddScoped<GetIncomesByPeriodUseCase>();
        services.AddScoped<GetIncomeByIdUseCase>();
        services.AddScoped<CreateIncomeUseCase>();
        services.AddScoped<DeleteIncomeUseCase>();

        // ── Import ────────────────────────────────────────────────────────────────────
        services.AddScoped<ImportLegacyDataUseCase>();

        return services;
    }
}
