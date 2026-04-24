# TestWebApplicationFactory — 3 substituições obrigatórias

```csharp
// 1. Remove TODOS os descritores do AppDbContext
var dbDescriptors = services.Where(d =>
    d.ServiceType == typeof(DbContextOptions<AppDbContext>) ||
    d.ServiceType == typeof(AppDbContext) ||
    (d.ServiceType.IsGenericType &&
     d.ServiceType.GenericTypeArguments.Contains(typeof(AppDbContext))))
    .ToList();
foreach (var d in dbDescriptors) services.Remove(d);
services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(_dbName));
// _dbName gerado como campo da factory — NUNCA dentro do lambda

// 2. IReportRepository → FakeReportRepository (SqlQueryRaw é relacional)
// 3. DatabaseInitializer → remover por ImplementationType?.Name == "DatabaseInitializer"
```
