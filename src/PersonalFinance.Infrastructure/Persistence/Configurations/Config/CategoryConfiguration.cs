using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonalFinance.Domain.Entities.Config;
using PersonalFinance.Infrastructure.Persistence.Configurations;

namespace PersonalFinance.Infrastructure.Persistence.Configurations.Config;

/// <summary>
/// Configuração EF Core da entidade Category.
/// UserId nullable: NULL = categoria global (admin).
/// </summary>
public sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Category");

        // Colunas do EntityBase
        builder.ApplyEntityBaseConfiguration();

        // UserId nullable — NULL = categoria global
        builder.Property(c => c.UserId)
               .HasColumnName("UserId")
               .HasColumnType("uniqueidentifier")
               .IsRequired(false);

        builder.Property(c => c.Name)
               .HasColumnName("Name")
               .HasColumnType("nvarchar(100)")
               .IsRequired();

        builder.Property(c => c.Color)
               .HasColumnName("Color")
               .HasColumnType("nvarchar(7)")
               .IsRequired();

        builder.Property(c => c.Icon)
               .HasColumnName("Icon")
               .HasColumnType("nvarchar(MAX)")
               .IsRequired(false);

        builder.Property(c => c.IsGlobal)
               .HasColumnName("IsGlobal")
               .HasColumnType("bit")
               .IsRequired()
               .HasDefaultValue(false);

        // FK → User (nullable — categorias globais não têm dono)
        builder.HasOne<Domain.Entities.Auth.User>()
               .WithMany()
               .HasForeignKey(c => c.UserId)
               .IsRequired(false)
               .OnDelete(DeleteBehavior.Restrict);

        // Índices filtrados
        builder.HasIndex(c => c.UserId)
               .HasFilter("[DeletedAt] IS NULL")
               .HasDatabaseName("IX_Category_UserId");

        builder.HasIndex(c => c.IsGlobal)
               .HasFilter("[DeletedAt] IS NULL")
               .HasDatabaseName("IX_Category_IsGlobal");
    }
}
