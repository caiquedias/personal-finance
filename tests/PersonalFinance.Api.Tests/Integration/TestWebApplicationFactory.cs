using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonalFinance.Application.Interfaces;
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
            // Substitui o DbContext real por InMemory
            var dbDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));

            if (dbDescriptor is not null)
                services.Remove(dbDescriptor);

            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase(_dbName));

            // Substitui ReportRepository pelo fake compatível com InMemory.
            // O real usa Database.SqlQueryRaw (relacional) — explode com InMemory.
            var reportDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IReportRepository));

            if (reportDescriptor is not null)
                services.Remove(reportDescriptor);

            services.AddScoped<IReportRepository, FakeReportRepository>();

            // Remove o DatabaseInitializer — ele chama MigrateAsync() e
            // ExecuteSqlRawAsync() que são métodos relacionais e explodem com InMemory.
            var initDescriptor = services.SingleOrDefault(
                d => d.ImplementationType?.Name == "DatabaseInitializer");

            if (initDescriptor is not null)
                services.Remove(initDescriptor);
        });
    }
}


