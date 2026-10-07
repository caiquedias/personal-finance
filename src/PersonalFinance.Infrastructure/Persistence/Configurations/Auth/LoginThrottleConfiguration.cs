using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonalFinance.Domain.Entities.Auth;

namespace PersonalFinance.Infrastructure.Persistence.Configurations.Auth;

/// <summary>
/// Configuração EF Core de LoginThrottle.
/// EXCEÇÃO deliberada ao soft-delete universal: tabela efêmera (retenção = janela do lockout; o IP é
/// dado pessoal), por isso sem DeletedAt e sem HasQueryFilter — a exclusão é física.
/// </summary>
public sealed class LoginThrottleConfiguration : IEntityTypeConfiguration<LoginThrottle>
{
    public void Configure(EntityTypeBuilder<LoginThrottle> builder)
    {
        builder.ToTable("LoginThrottle");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();

        builder.Property(t => t.UserId).IsRequired();

        builder.Property(t => t.IpAddress)
               .HasColumnType("nvarchar(45)")
               .HasMaxLength(45)
               .IsRequired();

        builder.Property(t => t.FailedCount).HasColumnType("int").IsRequired();
        builder.Property(t => t.WindowStart).HasColumnType("datetime2(7)").IsRequired();
        builder.Property(t => t.LockedUntil).HasColumnType("datetime2(7)");

        builder.Property(t => t.RowVersion).IsRowVersion();

        // Uma linha por par (conta, IP) — inserts concorrentes violam o índice e viram conflito de concorrência
        builder.HasIndex(t => new { t.UserId, t.IpAddress })
               .IsUnique()
               .HasDatabaseName("IX_LoginThrottle_UserId_IpAddress");

        // Apoia a limpeza por tempo
        builder.HasIndex(t => t.WindowStart)
               .HasDatabaseName("IX_LoginThrottle_WindowStart");

        builder.HasOne<User>()
               .WithMany()
               .HasForeignKey(t => t.UserId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
