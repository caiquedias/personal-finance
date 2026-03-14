using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonalFinance.Domain.Entities.Shared;

namespace PersonalFinance.Infrastructure.Persistence.Configurations;

/// <summary>
/// Método de extensão que aplica o mapeamento das colunas do EntityBase
/// em qualquer IEntityTypeConfiguration filha.
/// Uso: builder.ApplyEntityBaseConfiguration()
/// </summary>
public static class EntityBaseConfiguration
{
    public static void ApplyEntityBaseConfiguration<TEntity>(
        this EntityTypeBuilder<TEntity> builder)
        where TEntity : EntityBase
    {
        // PK — GUID gerado no domínio
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
               .HasColumnName("Id")
               .HasColumnType("uniqueidentifier")
               .ValueGeneratedNever(); // Domínio gera o Guid, não o banco

        builder.Property(e => e.CreatedAt)
               .HasColumnName("CreatedAt")
               .HasColumnType("datetime2(7)")
               .IsRequired();

        builder.Property(e => e.UpdatedAt)
               .HasColumnName("UpdatedAt")
               .HasColumnType("datetime2(7)")
               .IsRequired();

        builder.Property(e => e.DeletedAt)
               .HasColumnName("DeletedAt")
               .HasColumnType("datetime2(7)")
               .IsRequired(false);  // NULL = ativo

        builder.Property(e => e.IsActive)
               .HasColumnName("IsActive")
               .HasColumnType("bit")
               .IsRequired()
               .HasDefaultValue(true);

        // IsDeleted é calculado — sem coluna no banco
        builder.Ignore(e => e.IsDeleted);
    }
}
