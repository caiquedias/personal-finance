using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using PersonalFinance.Domain.Entities.Auth;
using PersonalFinance.Domain.Enums;

namespace PersonalFinance.Infrastructure.Persistence.Configurations.Auth;

/// <summary>
/// Configuração EF Core de AuditLog.
/// EXCEÇÃO deliberada ao soft-delete universal: trilha insert-only, sem DeletedAt e sem HasQueryFilter.
/// </summary>
public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLog");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.ActorUserId).IsRequired();
        builder.Property(a => a.TargetUserId).IsRequired();
        // Conversor explícito enum→int (HasConversion<int>() sozinho não registra ValueConverter para enums)
        builder.Property(a => a.Action)
               .HasConversion(new EnumToNumberConverter<AuditAction, int>())
               .IsRequired();

        builder.Property(a => a.Details).HasColumnType("nvarchar(2000)").HasMaxLength(2000);
        builder.Property(a => a.IpAddress).HasColumnType("nvarchar(45)").HasMaxLength(45);
        builder.Property(a => a.CreatedAt).HasColumnType("datetime2(7)").IsRequired();

        // Apoia o purge por retenção e a consulta por alvo
        builder.HasIndex(a => a.CreatedAt).HasDatabaseName("IX_AuditLog_CreatedAt");
        builder.HasIndex(a => a.TargetUserId).HasDatabaseName("IX_AuditLog_TargetUserId");

        builder.HasOne<User>()
               .WithMany()
               .HasForeignKey(a => a.ActorUserId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
               .WithMany()
               .HasForeignKey(a => a.TargetUserId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
