using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonalFinance.Domain.Entities.Auth;
using PersonalFinance.Domain.Entities.Config;
using PersonalFinance.Domain.Entities.Financial;
using PersonalFinance.Domain.Enums;
using PersonalFinance.Infrastructure.Persistence.Configurations;

namespace PersonalFinance.Infrastructure.Persistence.Configurations.Financial;

// ══════════════════════════════════════════════════════════════════════════════
// PERIOD
// ══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Configuração EF Core da entidade Period.
/// Unique constraint em (UserId, Year, Month).
/// </summary>
public sealed class PeriodConfiguration : IEntityTypeConfiguration<Period>
{
    public void Configure(EntityTypeBuilder<Period> builder)
    {
        builder.ToTable("Period");

        builder.ApplyEntityBaseConfiguration();

        builder.Property(p => p.UserId)
               .HasColumnName("UserId")
               .HasColumnType("uniqueidentifier")
               .IsRequired();

        builder.Property(p => p.Year)
               .HasColumnName("Year")
               .HasColumnType("smallint")
               .IsRequired();

        builder.Property(p => p.Month)
               .HasColumnName("Month")
               .HasColumnType("tinyint")
               .IsRequired();

        // CHECK constraint — mês 1-12
        builder.ToTable(t => t.HasCheckConstraint(
            "CK_Period_Month", "[Month] BETWEEN 1 AND 12"));

        // FK → User
        builder.HasOne<User>()
               .WithMany()
               .HasForeignKey(p => p.UserId)
               .OnDelete(DeleteBehavior.Restrict);

        // UNIQUE: um período por usuário/mês/ano
        builder.HasIndex(p => new { p.UserId, p.Year, p.Month })
               .IsUnique()
               .HasDatabaseName("UQ_Period_UserYearMonth");

        // Índices de performance
        builder.HasIndex(p => p.UserId)
               .HasFilter("[DeletedAt] IS NULL")
               .HasDatabaseName("IX_Period_UserId");

        builder.HasIndex(p => new { p.Year, p.Month })
               .HasDatabaseName("IX_Period_YearMonth");
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// EXPENSE
// ══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Configuração EF Core da entidade Expense.
/// Enums mapeados para int — preserva os valores de seed da lookup table.
/// CHECK constraint em Amount > 0.
/// </summary>
public sealed class ExpenseConfiguration : IEntityTypeConfiguration<Expense>
{
    public void Configure(EntityTypeBuilder<Expense> builder)
    {
        builder.ToTable("Expense");

        builder.ApplyEntityBaseConfiguration();

        builder.Property(e => e.PeriodId)
               .HasColumnName("PeriodId")
               .HasColumnType("uniqueidentifier")
               .IsRequired();

        builder.Property(e => e.UserId)
               .HasColumnName("UserId")
               .HasColumnType("uniqueidentifier")
               .IsRequired();

        builder.Property(e => e.CategoryId)
               .HasColumnName("CategoryId")
               .HasColumnType("uniqueidentifier")
               .IsRequired();

        // Enums mapeados como int — alinha com as lookup tables de seed
        builder.Property(e => e.SourceType)
               .HasColumnName("SourceTypeId")
               .HasColumnType("int")
               .IsRequired()
               .HasConversion<int>();

        builder.Property(e => e.FortnightType)
               .HasColumnName("FortnightTypeId")
               .HasColumnType("int")
               .IsRequired()
               .HasConversion<int>();

        builder.Property(e => e.PaymentStatus)
               .HasColumnName("PaymentStatusId")
               .HasColumnType("int")
               .IsRequired()
               .HasConversion<int>();

        builder.Property(e => e.Description)
               .HasColumnName("Description")
               .HasColumnType("nvarchar(200)")
               .IsRequired();

        builder.Property(e => e.Amount)
               .HasColumnName("Amount")
               .HasColumnType("decimal(18,2)")
               .IsRequired();

        builder.Property(e => e.DueDate)
               .HasColumnName("DueDate")
               .HasColumnType("date")
               .IsRequired();

        builder.Property(e => e.PaymentDate)
               .HasColumnName("PaymentDate")
               .HasColumnType("date")
               .IsRequired(false);

        builder.Property(e => e.Notes)
               .HasColumnName("Notes")
               .HasColumnType("nvarchar(500)")
               .IsRequired(false);

        // CHECK constraint — valor sempre positivo
        builder.ToTable(t => t.HasCheckConstraint(
            "CK_Expense_Amount", "[Amount] > 0"));

        // FKs
        builder.HasOne<Period>()
               .WithMany()
               .HasForeignKey(e => e.PeriodId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
               .WithMany()
               .HasForeignKey(e => e.UserId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Category>()
               .WithMany()
               .HasForeignKey(e => e.CategoryId)
               .OnDelete(DeleteBehavior.Restrict);

        // Índices
        builder.HasIndex(e => e.PeriodId)
               .HasFilter("[DeletedAt] IS NULL")
               .HasDatabaseName("IX_Expense_PeriodId");

        builder.HasIndex(e => e.UserId)
               .HasFilter("[DeletedAt] IS NULL")
               .HasDatabaseName("IX_Expense_UserId");

        builder.HasIndex(e => e.CategoryId)
               .HasFilter("[DeletedAt] IS NULL")
               .HasDatabaseName("IX_Expense_CategoryId");

        builder.HasIndex(e => e.PaymentStatus)
               .HasFilter("[DeletedAt] IS NULL")
               .HasDatabaseName("IX_Expense_PaymentStatusId");

        builder.HasIndex(e => e.DueDate)
               .HasDatabaseName("IX_Expense_DueDate");
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// EXPENSE ORDER
// ══════════════════════════════════════════════════════════════════════════════

public sealed class ExpenseOrderConfiguration : IEntityTypeConfiguration<ExpenseOrder>
{
    public void Configure(EntityTypeBuilder<ExpenseOrder> builder)
    {
        builder.ToTable("ExpenseOrder");

        builder.ApplyEntityBaseConfiguration();

        builder.Property(o => o.ExpenseId)
               .HasColumnName("ExpenseId")
               .HasColumnType("uniqueidentifier")
               .IsRequired();

        builder.Property(o => o.Order)
               .HasColumnName("Order")
               .HasColumnType("int")
               .IsRequired();

        builder.HasOne<Expense>()
               .WithMany()
               .HasForeignKey(o => o.ExpenseId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(o => o.ExpenseId)
               .IsUnique()
               .HasFilter("[DeletedAt] IS NULL")
               .HasDatabaseName("UQ_ExpenseOrder_ExpenseId");
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// INCOME
// ══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Configuração EF Core da entidade Income.
/// CHECK constraint em Amount > 0.
/// </summary>
public sealed class IncomeConfiguration : IEntityTypeConfiguration<Income>
{
    public void Configure(EntityTypeBuilder<Income> builder)
    {
        builder.ToTable("Income");

        builder.ApplyEntityBaseConfiguration();

        builder.Property(i => i.PeriodId)
               .HasColumnName("PeriodId")
               .HasColumnType("uniqueidentifier")
               .IsRequired();

        builder.Property(i => i.UserId)
               .HasColumnName("UserId")
               .HasColumnType("uniqueidentifier")
               .IsRequired();

        builder.Property(i => i.FortnightType)
               .HasColumnName("FortnightTypeId")
               .HasColumnType("int")
               .IsRequired()
               .HasConversion<int>();

        builder.Property(i => i.Description)
               .HasColumnName("Description")
               .HasColumnType("nvarchar(200)")
               .IsRequired();

        builder.Property(i => i.Amount)
               .HasColumnName("Amount")
               .HasColumnType("decimal(18,2)")
               .IsRequired();

        builder.Property(i => i.ReceivedAt)
               .HasColumnName("ReceivedAt")
               .HasColumnType("date")
               .IsRequired();

        builder.Property(i => i.Notes)
               .HasColumnName("Notes")
               .HasColumnType("nvarchar(500)")
               .IsRequired(false);

        // CHECK constraint
        builder.ToTable(t => t.HasCheckConstraint(
            "CK_Income_Amount", "[Amount] > 0"));

        // FKs
        builder.HasOne<Period>()
               .WithMany()
               .HasForeignKey(i => i.PeriodId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
               .WithMany()
               .HasForeignKey(i => i.UserId)
               .OnDelete(DeleteBehavior.Restrict);

        // Índices
        builder.HasIndex(i => i.PeriodId)
               .HasFilter("[DeletedAt] IS NULL")
               .HasDatabaseName("IX_Income_PeriodId");

        builder.HasIndex(i => i.UserId)
               .HasFilter("[DeletedAt] IS NULL")
               .HasDatabaseName("IX_Income_UserId");
    }
}
