using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonalFinance.Domain.Entities.Lookup;

namespace PersonalFinance.Infrastructure.Persistence.Configurations.Lookup;

// ══════════════════════════════════════════════════════════════════════════════
// ROLE
// ══════════════════════════════════════════════════════════════════════════════

public sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("Role");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
               .HasColumnName("Id")
               .HasColumnType("int")
               .ValueGeneratedNever();

        builder.Property(r => r.Name)
               .HasColumnName("Name")
               .HasColumnType("nvarchar(50)")
               .IsRequired();

        builder.Property(r => r.Description)
               .HasColumnName("Description")
               .HasColumnType("nvarchar(200)")
               .IsRequired();

        // Seed
        builder.HasData(
            new Role { Id = 1, Name = "Admin", Description = "Administrador do sistema — acesso total"      },
            new Role { Id = 2, Name = "User",  Description = "Usuário padrão — acesso às próprias finanças" }
        );
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// SOURCE TYPE
// ══════════════════════════════════════════════════════════════════════════════

public sealed class SourceTypeConfiguration : IEntityTypeConfiguration<SourceType>
{
    public void Configure(EntityTypeBuilder<SourceType> builder)
    {
        builder.ToTable("SourceType");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
               .HasColumnName("Id")
               .HasColumnType("int")
               .ValueGeneratedNever();

        builder.Property(s => s.Name)
               .HasColumnName("Name")
               .HasColumnType("nvarchar(50)")
               .IsRequired();

        // Seed — alinhado com o enum SourceType do Domain
        builder.HasData(
            new SourceType { Id = 1, Name = "Parental" }, // Despesas relacionadas aos pais
            new SourceType { Id = 2, Name = "Personal" }  // Despesas próprias do usuário
        );
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// FORTNIGHT TYPE
// ══════════════════════════════════════════════════════════════════════════════

public sealed class FortnightTypeConfiguration : IEntityTypeConfiguration<FortnightType>
{
    public void Configure(EntityTypeBuilder<FortnightType> builder)
    {
        builder.ToTable("FortnightType");

        builder.HasKey(f => f.Id);

        builder.Property(f => f.Id)
               .HasColumnName("Id")
               .HasColumnType("int")
               .ValueGeneratedNever();

        builder.Property(f => f.Name)
               .HasColumnName("Name")
               .HasColumnType("nvarchar(50)")
               .IsRequired();

        // Seed — alinhado com o enum FortnightType do Domain
        builder.HasData(
            new FortnightType { Id = 1, Name = "First"  }, // 1ª quinzena: dias 1–15
            new FortnightType { Id = 2, Name = "Second" }  // 2ª quinzena: dias 16–fim do mês
        );
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// PAYMENT STATUS
// ══════════════════════════════════════════════════════════════════════════════

public sealed class PaymentStatusConfiguration : IEntityTypeConfiguration<PaymentStatus>
{
    public void Configure(EntityTypeBuilder<PaymentStatus> builder)
    {
        builder.ToTable("PaymentStatus");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
               .HasColumnName("Id")
               .HasColumnType("int")
               .ValueGeneratedNever();

        builder.Property(p => p.Name)
               .HasColumnName("Name")
               .HasColumnType("nvarchar(50)")
               .IsRequired();

        builder.Property(p => p.Description)
               .HasColumnName("Description")
               .HasColumnType("nvarchar(200)")
               .IsRequired();

        // Seed — alinhado com o enum PaymentStatus do Domain
        builder.HasData(
            new PaymentStatus { Id = 1, Name = "Pending",   Description = "Despesa pendente de pagamento"   },
            new PaymentStatus { Id = 2, Name = "Paid",      Description = "Despesa paga"                    },
            new PaymentStatus { Id = 3, Name = "Cancelled", Description = "Despesa cancelada"               },
            new PaymentStatus { Id = 4, Name = "Partial",   Description = "Despesa parcialmente paga"       }
        );
    }
}
