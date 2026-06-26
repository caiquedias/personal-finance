using Microsoft.EntityFrameworkCore;
using PersonalFinance.Domain.Entities.Auth;
using PersonalFinance.Domain.Entities.Config;
using PersonalFinance.Domain.Entities.Financial;
using PersonalFinance.Domain.Entities.Lookup;

namespace PersonalFinance.Infrastructure.Persistence.Context;

/// <summary>
/// DbContext principal do sistema.
/// HasQueryFilter global em todas as entidades de domínio filtra DeletedAt IS NULL.
/// Lookup tables não possuem soft delete — sem HasQueryFilter nelas.
/// </summary>
public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options) { }

    // ── Entidades de domínio ──────────────────────────────────────────────────
    public DbSet<User> Users { get; set; } = default!;
    public DbSet<UserRole> UserRoles { get; set; } = default!;
    public DbSet<Category> Categories { get; set; } = default!;
    public DbSet<Period> Periods { get; set; } = default!;
    public DbSet<Expense> Expenses { get; set; } = default!;
    public DbSet<ExpenseOrder> ExpenseOrders { get; set; } = default!;
    public DbSet<Income> Incomes { get; set; } = default!;
    public DbSet<PurgeRecord> PurgeRecords { get; set; } = default!;

    // ── Lookup tables (seed) ──────────────────────────────────────────────────
    public DbSet<Role> Roles { get; set; } = default!;
    public DbSet<SourceType> SourceTypes { get; set; } = default!;
    public DbSet<FortnightType> FortnightTypes { get; set; } = default!;
    public DbSet<PaymentStatus> PaymentStatuses { get; set; } = default!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Carrega todas as IEntityTypeConfiguration do assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // HasQueryFilter global — filtra DeletedAt IS NULL nas entidades de domínio
        modelBuilder.Entity<User>().HasQueryFilter(e => e.DeletedAt == null);
        modelBuilder.Entity<Category>().HasQueryFilter(e => e.DeletedAt == null);
        modelBuilder.Entity<Period>().HasQueryFilter(e => e.DeletedAt == null);
        modelBuilder.Entity<Expense>().HasQueryFilter(e => e.DeletedAt == null);
        modelBuilder.Entity<ExpenseOrder>().HasQueryFilter(e => e.DeletedAt == null);
        modelBuilder.Entity<Income>().HasQueryFilter(e => e.DeletedAt == null);
        modelBuilder.Entity<PurgeRecord>().HasQueryFilter(e => e.DeletedAt == null);
    }

    // ── Intercepta SaveChanges — atualiza UpdatedAt ───────────────────────────
    public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        UpdateTimestamps();
        return await base.SaveChangesAsync(ct);
    }

    public override int SaveChanges()
    {
        UpdateTimestamps();
        return base.SaveChanges();
    }

    private void UpdateTimestamps()
    {
        var entries = ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Modified);

        foreach (var entry in entries)
        {
            var method = entry.Entity.GetType().GetMethod("SetUpdatedAt");
            method?.Invoke(entry.Entity, null);
        }
    }
}
