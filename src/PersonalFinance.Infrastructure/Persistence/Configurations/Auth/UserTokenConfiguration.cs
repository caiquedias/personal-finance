using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonalFinance.Domain.Entities.Auth;

namespace PersonalFinance.Infrastructure.Persistence.Configurations.Auth;

/// <summary>
/// Configuração EF Core de UserToken.
/// EXCEÇÃO deliberada ao soft-delete universal: o código guarda só hash e os anteriores do mesmo
/// (usuário, propósito) são apagados fisicamente a cada emissão — sem DeletedAt e sem HasQueryFilter.
/// </summary>
public sealed class UserTokenConfiguration : IEntityTypeConfiguration<UserToken>
{
    public void Configure(EntityTypeBuilder<UserToken> builder)
    {
        builder.ToTable("UserToken");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();

        builder.Property(t => t.UserId).IsRequired();

        builder.Property(t => t.Purpose)
               .HasConversion<int>()
               .IsRequired();

        // HMAC-SHA256 em Base64 (44 chars) — nunca o código em claro
        builder.Property(t => t.TokenHash)
               .HasColumnType("nvarchar(128)")
               .HasMaxLength(128)
               .IsRequired();

        builder.Property(t => t.CreatedAt).HasColumnType("datetime2(7)").IsRequired();
        builder.Property(t => t.ExpiresAt).HasColumnType("datetime2(7)").IsRequired();
        builder.Property(t => t.UsedAt).HasColumnType("datetime2(7)");

        builder.Property(t => t.Attempts)
               .HasColumnType("int")
               .HasDefaultValue(0)
               .IsRequired();

        builder.Property(t => t.IsInvalidated)
               .HasColumnType("bit")
               .HasDefaultValue(false)
               .IsRequired();

        // Concorrência otimista: protege o contador de tentativas erradas
        builder.Property(t => t.RowVersion).IsRowVersion();

        builder.HasIndex(t => new { t.UserId, t.Purpose })
               .HasDatabaseName("IX_UserToken_UserId_Purpose");

        builder.HasOne<User>()
               .WithMany()
               .HasForeignKey(t => t.UserId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
