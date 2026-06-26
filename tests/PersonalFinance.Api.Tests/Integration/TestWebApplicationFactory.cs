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


