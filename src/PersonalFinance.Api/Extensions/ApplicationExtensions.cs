using FluentValidation;
using Microsoft.Extensions.Options;
using PersonalFinance.Application.Interfaces;
using PersonalFinance.Application.Options;
using PersonalFinance.Application.Services.Auth;
using PersonalFinance.Application.UseCases.Admin;
using PersonalFinance.Application.UseCases.Auth;
using PersonalFinance.Application.UseCases.Config;
using PersonalFinance.Application.UseCases.Financial.Expenses;
using PersonalFinance.Application.UseCases.Financial.Incomes;
using PersonalFinance.Application.UseCases.Financial.Periods;
using PersonalFinance.Application.UseCases.Financial.Purge;
using PersonalFinance.Application.UseCases.Import;
using PersonalFinance.Application.UseCases.Reports;
using PersonalFinance.Infrastructure.Auth;
using PersonalFinance.Infrastructure.Services;

namespace PersonalFinance.Api.Extensions;

public static class ApplicationExtensions
{
    public static IServiceCollection AddApplicationUseCases(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ── Auth ──────────────────────────────────────────────────────────────
        // Lockout de login: valores de Auth:LoginLockout, com defaults 5 tentativas / 15 min
        // Validado no startup (ValidateOnStart): config inválida falha ao subir, com mensagem clara
        services.AddOptions<LoginLockoutOptions>()
            .Bind(configuration.GetSection("Auth:LoginLockout"))
            .Validate(o => o.MaxFailedAttempts >= 1,
                "Auth:LoginLockout:MaxFailedAttempts deve ser >= 1.")
            .Validate(o => o.LockoutMinutes >= 1,
                "Auth:LoginLockout:LockoutMinutes deve ser >= 1.")
            .Validate(o => o.GlobalMaxFailedAttempts >= o.MaxFailedAttempts,
                "Auth:LoginLockout:GlobalMaxFailedAttempts deve ser >= MaxFailedAttempts.")
            .ValidateOnStart();
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<LoginLockoutOptions>>().Value);

        // MFA/TOTP (#393): Auth:Mfa. A EncryptionKey é validada SEMPRE no startup (independente de Enforce);
        // a mensagem nunca inclui o valor da chave.
        services.AddOptions<MfaOptions>()
            .Bind(configuration.GetSection("Auth:Mfa"))
            .Validate(o => AesGcmSecretProtector.TryParseKey(o.EncryptionKey, out _),
                "Auth:Mfa:EncryptionKey é obrigatória e deve ser Base64 de exatamente 32 bytes.")
            .ValidateOnStart();
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<MfaOptions>>().Value);

        // Verificação de e-mail (#404): Auth:EmailVerification:Enforce (default false)
        services.AddOptions<EmailVerificationOptions>()
            .Bind(configuration.GetSection("Auth:EmailVerification"));
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<EmailVerificationOptions>>().Value);

        // Validators FluentValidation (issue 396): registra todos os IValidator<T> da assembly Application
        services.AddValidatorsFromAssemblyContaining<RegisterUserUseCase>(ServiceLifetime.Scoped);

        services.AddScoped<RegisterUserUseCase>();
        services.AddScoped<LoginWithRolesUseCase>(); // substitui LoginUseCase
        services.AddScoped<SetupMfaUseCase>();
        services.AddScoped<EnableMfaUseCase>();
        services.AddScoped<DisableMfaUseCase>();
        services.AddScoped<VerifyMfaUseCase>();

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
        services.AddScoped<ResetUserMfaUseCase>();
        // Audit log (#402): retenção AuditLog:Retention, validada no startup
        services.AddOptions<AuditLogRetentionOptions>()
            .Bind(configuration.GetSection("AuditLog:Retention"))
            .Validate(o => o.RetentionDays >= 1, "AuditLog:Retention:RetentionDays deve ser >= 1.")
            .Validate(o => o.PurgeIntervalMinutes >= 1, "AuditLog:Retention:PurgeIntervalMinutes deve ser >= 1.")
            .Validate(o => o.BatchSize >= 1, "AuditLog:Retention:BatchSize deve ser >= 1.")
            .ValidateOnStart();
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<AuditLogRetentionOptions>>().Value);
        services.AddScoped<PurgeExpiredAuditLogsUseCase>();
        services.AddHostedService<PersonalFinance.Api.BackgroundServices.AuditLogPurgeHostedService>();
        // Pipeline de e-mail (#404): fila em memória + dispatcher + Brevo (typed HttpClient).
        // Validação de startup de App:FrontendBaseUrl e demais options entra na task 11.
        var appOptions = new AppOptions();
        configuration.GetSection("App").Bind(appOptions);
        services.AddSingleton(appOptions);
        var emailOptions = new EmailOptions();
        configuration.GetSection("Email").Bind(emailOptions);
        services.AddSingleton(emailOptions);
        var brevoOptions = new BrevoOptions();
        configuration.GetSection("Email:Brevo").Bind(brevoOptions);
        services.AddSingleton(brevoOptions);
        services.AddSingleton<AuthEmailComposer>();
        services.AddHttpClient<IEmailSender, BrevoEmailSender>();
        services.AddHostedService<PersonalFinance.Api.BackgroundServices.EmailDispatchHostedService>();
        services.AddScoped<CreateUserByAdminUseCase>();
        services.AddScoped<UpdateUserByAdminUseCase>();

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
        services.AddScoped<CreateExpensesBatchUseCase>();
        services.AddScoped<UpdateExpenseUseCase>();
        services.AddScoped<DeleteExpenseUseCase>();
        services.AddScoped<DeleteExpensesBatchUseCase>();
        services.AddScoped<PayExpensesBatchUseCase>();
        services.AddScoped<CancelExpensesBatchUseCase>();
        services.AddScoped<SaveExpenseOrderUseCase>();
        services.AddScoped<GetRecurringExpensesFromLastPeriodUseCase>();
        services.AddScoped<ReplicateExpensesUseCase>();

        // ── Financial — Incomes ───────────────────────────────────────────────
        services.AddScoped<GetIncomesByPeriodUseCase>();
        services.AddScoped<GetIncomeByIdUseCase>();
        services.AddScoped<CreateIncomeUseCase>();
        services.AddScoped<UpdateIncomeUseCase>();
        services.AddScoped<DeleteIncomeUseCase>();

        // ── Import ────────────────────────────────────────────────────────────────────
        services.AddScoped<ImportLegacyDataUseCase>();
        services.AddScoped<StatementEntryClassifier>();
        services.AddScoped<PreviewStatementImportUseCase>();
        services.AddScoped<ConfirmStatementImportUseCase>();

        // ── Reports ───────────────────────────────────────────────────────────
        services.AddScoped<GetExpensesReportUseCase>();

        // ── Purge ─────────────────────────────────────────────────────────────
        services.AddScoped<GetEligiblePeriodsUseCase>();
        services.AddScoped<ExportPeriodUseCase>();
        services.AddScoped<PurgePeriodUseCase>();
        services.AddScoped<GetPurgeRecordsUseCase>();
        services.AddScoped<DeletePurgeRecordUseCase>();

        return services;
    }
}
