using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonalFinance.Domain.Entities.Auth;

namespace PersonalFinance.Infrastructure.Persistence.Configurations.Auth;

/// <summary>
/// Configuração EF Core de MfaRecoveryCode. Soft-delete universal (query filter no AppDbContext).
/// </summary>
public sealed class MfaRecoveryCodeConfiguration : IEntityTypeConfiguration<MfaRecoveryCode>
{
    public void Configure(EntityTypeBuilder<MfaRecoveryCode> builder)
    {
        builder.ToTable("MfaRecoveryCode");

        builder.ApplyEntityBaseConfiguration();

        builder.Property(c => c.UserId).IsRequired();

        // Hash Argon2id (Base64(salt):Base64(hash)) — nunca o código em claro
        builder.Property(c => c.CodeHash)
               .HasColumnType("nvarchar(512)")
               .HasMaxLength(512)
               .IsRequired();

        builder.Property(c => c.UsedAt).HasColumnType("datetime2(7)");

        builder.HasIndex(c => c.UserId)
               .HasDatabaseName("IX_MfaRecoveryCode_UserId");

        builder.HasOne<User>()
               .WithMany()
               .HasForeignKey(c => c.UserId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
