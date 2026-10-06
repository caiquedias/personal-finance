using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonalFinance.Api.Tests.Integration.Fakes;
using PersonalFinance.Application.Interfaces;
using PersonalFinance.Domain.Entities.Auth;
using PersonalFinance.Domain.Entities.Lookup;
using PersonalFinance.Domain.Interfaces.Repositories;
using PersonalFinance.Domain.Interfaces.Services;
using PersonalFinance.Infrastructure.Persistence.Context;


namespace PersonalFinance.Api.Tests.Integration;

/// <summary>
/// Substitui o SQL Server por InMemory para os testes de integração.
/// Banco é resetado a cada instância da factory.
/// </summary>
public sealed class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    // Nome gerado UMA vez por instância — todas as requests compartilham o mesmo banco.
    // Se estivesse dentro do lambda do AddDbContext, Guid.NewGuid() seria chamado
    // a cada request (DbContext é Scoped), criando um banco vazio por request.
    private readonly string _dbName = $"TestDb_{Guid.NewGuid()}";

    // Secret fixo apenas para infraestrutura de testes — não é segredo real.
    // appsettings.json não deve conter SecretKey em texto puro (ver #389),
    // então injetamos aqui via configuração in-memory.
    private const string TestJwtSecretKey = "TestOnly_9f8e7d6c5b4a3210_FakeJwtSecretKey_NotForProd";

    // Program.cs (minimal hosting) lê builder.Configuration.GetSection("JwtSettings")
    // de forma síncrona, durante a construção do WebApplicationBuilder — antes de
    // qualquer callback ConfigureAppConfiguration/ConfigureServices registrado via
    // ConfigureWebHost ter chance de rodar. Por isso a variável de ambiente é setada
    // no construtor estático (roda uma única vez, antes da primeira instância da
    // factory ser usada) — AddEnvironmentVariables() é uma fonte de configuração
    // padrão do WebApplicationBuilder e tem precedência sobre appsettings.json.
    static TestWebApplicationFactory()
    {
        Environment.SetEnvironmentVariable("JwtSettings__SecretKey", TestJwtSecretKey);

        // Limite alto por padrão: a suíte faz dezenas de logins do mesmo admin (mesmo "IP" nulo).
        // Testes do 429 sobrescrevem via WithWebHostBuilder (ver docs/testing.md, #391).
        Environment.SetEnvironmentVariable("RateLimiting__Login__PermitLimit", "100000");

        // MFA (#393): chave AES-256 de teste (Base64 de 32 bytes) — não é segredo real — e limite alto
        // para a policy mfa-verify. Testes do 429 sobrescrevem via WithWebHostBuilder.
        Environment.SetEnvironmentVariable("Auth__Mfa__EncryptionKey", TestMfaEncryptionKey);
        Environment.SetEnvironmentVariable("RateLimiting__MfaVerify__PermitLimit", "100000");

        // Audit log (#402): purge em background desligado nos testes (determinismo); o use case é testado à parte.
        Environment.SetEnvironmentVariable("AuditLog__Retention__Enabled", "false");

        // Reset de senha / verificação de e-mail (#404): valores dummy — nada disso é segredo real.
        // Sem HmacKey a app não sobe (ValidateOnStart); o envio real (Brevo) é trocado pelo FakeEmailSender.
        Environment.SetEnvironmentVariable("Auth__UserTokens__HmacKey", TestUserTokenHmacKey);
        Environment.SetEnvironmentVariable("Auth__EmailVerification__Enforce", "false");
        Environment.SetEnvironmentVariable("Email__Enabled", "true");
        Environment.SetEnvironmentVariable("Email__Brevo__ApiKey", "test-brevo-api-key-not-real");
        Environment.SetEnvironmentVariable("Email__Brevo__SenderEmail", "no-reply@monkeybomb.test");
        Environment.SetEnvironmentVariable("Email__Brevo__SenderName", "MonkeyBomb Test");
        Environment.SetEnvironmentVariable("App__FrontendBaseUrl", TestFrontendBaseUrl);
        // Limite alto para a policy account-recovery; testes do 429 sobrescrevem via WithWebHostBuilder.
        Environment.SetEnvironmentVariable("RateLimiting__AccountRecovery__PermitLimit", "100000");
    }

    // Base64 de 32 bytes (0x00..0x1F) — só para testes
    private const string TestMfaEncryptionKey = "AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8=";

    // Base64 de 32 bytes (0x20..0x3F) — só para testes (#404)
    private const string TestUserTokenHmacKey = "ICEiIyQlJicoKSorLC0uLzAxMjM0NTY3ODk6Ozw9Pj8=";

    public const string TestFrontendBaseUrl = "https://app.monkeybomb.test";

    /// <summary>
    /// Substitui o envio real de e-mail (#404). Instância única por factory — as factories derivadas
    /// (WithWebHostBuilder) reaproveitam esta mesma instância.
    /// </summary>
    public FakeEmailSender EmailSender { get; } = new();

    protected override void ConfigureWebHost(
        Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            var dbDescriptors = services
                .Where(d =>
                    d.ServiceType == typeof(DbContextOptions<AppDbContext>) ||
                    d.ServiceType == typeof(AppDbContext) ||
                    (d.ServiceType.IsGenericType &&
                     d.ServiceType.GenericTypeArguments.Contains(typeof(AppDbContext))))
                .ToList();

            foreach (var d in dbDescriptors)
                services.Remove(d);

            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase(_dbName));

            // Substitui ReportRepository pelo fake compatível com InMemory.
            // O real usa Database.SqlQueryRaw (relacional) — explode com InMemory.
            var reportDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IReportRepository));

            if (reportDescriptor is not null)
                services.Remove(reportDescriptor);

            services.AddScoped<IReportRepository, FakeReportRepository>();

            // Substitui PurgeRepository pelo fake compatível com InMemory.
            var purgeDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IPurgeRepository));

            if (purgeDescriptor is not null)
                services.Remove(purgeDescriptor);

            services.AddScoped<IPurgeRepository, FakePurgeRepository>();

            // Substitui o provedor de e-mail (Brevo via HTTP) pelo fake que só registra as mensagens (#404).
            foreach (var d in services.Where(d => d.ServiceType == typeof(IEmailSender)).ToList())
                services.Remove(d);

            services.AddSingleton<IEmailSender>(EmailSender);

            // Remove o DatabaseInitializer — ele chama MigrateAsync() e
            // ExecuteSqlRawAsync() que são métodos relacionais e explodem com InMemory.
            var initDescriptor = services.SingleOrDefault(
                d => d.ImplementationType?.Name == "DatabaseInitializer");

            if (initDescriptor is not null)
                services.Remove(initDescriptor);

            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var hasher  = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
            context.Database.EnsureCreated();
            SeedLookupData(context);
            SeedAdminUser(context, hasher);
        });
    }

    private static void SeedAdminUser(AppDbContext context, IPasswordHasher hasher)
    {
        const string adminEmail = "caique_dias@outlook.com";
        if (context.Users.Any(u => u.Email == adminEmail)) return;

        var admin = User.Create("Admin Test", adminEmail, hasher.Hash("Arkham@01"));
        admin.ConfirmEmail(DateTime.UtcNow); // admin semeado já tem e-mail verificado (#404)
        context.Users.Add(admin);
        context.SaveChanges();

        context.UserRoles.Add(new UserRole { UserId = admin.Id, RoleId = 1 });
        context.SaveChanges();
    }

    private static void SeedLookupData(AppDbContext context)
    {
        if (context.Roles.Any()) return; // idempotente — não resemeia se já existe

        context.Roles.AddRange(
            new Role { Id = 1, Name = "Admin", Description = "Administrador do sistema" },
            new Role { Id = 2, Name = "User", Description = "Usuário padrão" }
        );
        context.SourceTypes.AddRange(
            new SourceType { Id = 1, Name = "Parental" },
            new SourceType { Id = 2, Name = "Personal" }
        );
        context.FortnightTypes.AddRange(
            new FortnightType { Id = 1, Name = "First" },
            new FortnightType { Id = 2, Name = "Second" }
        );
        context.PaymentStatuses.AddRange(
            new PaymentStatus { Id = 1, Name = "Pending", Description = "Pendente" },
            new PaymentStatus { Id = 2, Name = "Paid", Description = "Pago" },
            new PaymentStatus { Id = 3, Name = "Cancelled", Description = "Cancelado" },
            new PaymentStatus { Id = 4, Name = "Partial", Description = "Parcialmente pago" }
        );
        context.SaveChanges();
    }
}


