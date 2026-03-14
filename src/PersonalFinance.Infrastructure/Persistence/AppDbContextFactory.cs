using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using PersonalFinance.Infrastructure.Persistence.Context;

namespace PersonalFinance.Infrastructure.Persistence;

/// <summary>
/// Factory usada exclusivamente pelo dotnet-ef em tempo de design
/// (Add-Migration, database update, etc.).
/// Nunca é chamada em produção — apenas pelas EF Core Tools.
/// Lê a connection string do appsettings.json do projeto Api.
/// </summary>
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        // Busca o appsettings.json do projeto Api — onde a connection string está configurada
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(),
                "..", "PersonalFinance.Api"))
            .AddJsonFile("appsettings.json",             optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:DefaultConnection não encontrada. " +
                "Verifique o appsettings.json em src/PersonalFinance.Api/");

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseSqlServer(connectionString);

        return new AppDbContext(optionsBuilder.Options);
    }
}
