using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonalFinance.Domain.Entities.Auth;
using PersonalFinance.Domain.Entities.Financial;
using PersonalFinance.Infrastructure.Persistence.Configurations;

namespace PersonalFinance.Infrastructure.Persistence.Configurations.Financial;

/// <summary>
/// Configuração EF Core da entidade PurgeRecord.
/// Soft-delete via HasQueryFilter global (DeletedAt == null).
/// </summary>
public sealed class PurgeRecordConfiguration : IEntityTypeConfiguration<PurgeRecord>
{
    public void Configure(EntityTypeBuilder<PurgeRecord> builder)
    {
        builder.ToTable("PurgeRecord");

        builder.ApplyEntityBaseConfiguration();

        builder.Property(r => r.UserId)
               .HasColumnName("UserId")
               .HasColumnType("uniqueidentifier")
               .IsRequired();

        builder.Property(r => r.PeriodYear)
               .HasColumnName("PeriodYear")
               .HasColumnType("smallint")
               .IsRequired();

        builder.Property(r => r.PeriodMonth)
               .HasColumnName("PeriodMonth")
               .HasColumnType("tinyint")
               .IsRequired();

        builder.Property(r => r.PurgedAt)
               .HasColumnName("PurgedAt")
               .HasColumnType("datetime2(7)")
               .IsRequired();

        builder.Property(r => r.TotalIncome)
               .HasColumnName("TotalIncome")
               .HasColumnType("decimal(18,2)")
               .IsRequired();

        builder.Property(r => r.TotalExpense)
               .HasColumnName("TotalExpense")
               .HasColumnType("decimal(18,2)")
               .IsRequired();

        builder.Property(r => r.ExpenseCount)
               .HasColumnName("ExpenseCount")
               .HasColumnType("int")
               .IsRequired();

        builder.Property(r => r.IncomeCount)
               .HasColumnName("IncomeCount")
               .HasColumnType("int")
               .IsRequired();

        builder.Property(r => r.CategorySummaryJson)
               .HasColumnName("CategorySummaryJson")
               .HasColumnType("nvarchar(4000)")
               .IsRequired();

        builder.Property(r => r.CsvFileName)
               .HasColumnName("CsvFileName")
               .HasColumnType("nvarchar(255)")
               .IsRequired();

        // FK → User
        builder.HasOne<User>()
               .WithMany()
               .HasForeignKey(r => r.UserId)
               .OnDelete(DeleteBehavior.Restrict);

        // Índices
        builder.HasIndex(r => r.UserId)
               .HasFilter("[DeletedAt] IS NULL")
               .HasDatabaseName("IX_PurgeRecord_UserId");
    }
}
