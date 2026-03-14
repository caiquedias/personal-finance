using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonalFinance.Domain.Entities.Auth;
using PersonalFinance.Infrastructure.Persistence.Configurations;

namespace PersonalFinance.Infrastructure.Persistence.Configurations.Auth;

/// <summary>
/// Configuração EF Core da entidade User.
/// Índice único em Email filtrado por DeletedAt IS NULL.
/// </summary>
public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("User");

        // Colunas do EntityBase
        builder.ApplyEntityBaseConfiguration();

        // Colunas próprias
        builder.Property(u => u.Name)
               .HasColumnName("Name")
               .HasColumnType("nvarchar(100)")
               .IsRequired();

        builder.Property(u => u.Email)
               .HasColumnName("Email")
               .HasColumnType("nvarchar(200)")
               .IsRequired();

        builder.Property(u => u.PasswordHash)
               .HasColumnName("PasswordHash")
               .HasColumnType("nvarchar(512)")
               .IsRequired();

        // Unique constraint em Email — filtrado por DeletedAt IS NULL no DDL
        builder.HasIndex(u => u.Email)
               .IsUnique()
               .HasFilter("[DeletedAt] IS NULL")
               .HasDatabaseName("IX_User_Email");

        // Índice de performance para consultas de usuários ativos
        builder.HasIndex(u => u.IsActive)
               .HasFilter("[DeletedAt] IS NULL")
               .HasDatabaseName("IX_User_IsActive");
    }
}

/// <summary>
/// Configuração EF Core da junction table UserRole.
/// Sem EntityBase — tabela de seed/associação simples.
/// </summary>
public sealed class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> builder)
    {
        builder.ToTable("UserRole");

        // PK composta
        builder.HasKey(ur => new { ur.UserId, ur.RoleId });

        builder.Property(ur => ur.UserId)
               .HasColumnName("UserId")
               .HasColumnType("uniqueidentifier")
               .IsRequired();

        builder.Property(ur => ur.RoleId)
               .HasColumnName("RoleId")
               .HasColumnType("int")
               .IsRequired();

        builder.Property(ur => ur.AssignedAt)
               .HasColumnName("AssignedAt")
               .HasColumnType("datetime2(7)")
               .IsRequired();

        // FK → User
        builder.HasOne<User>()
               .WithMany()
               .HasForeignKey(ur => ur.UserId)
               .OnDelete(DeleteBehavior.Restrict);

        // FK → Role (sem navegação — Role é tabela seed sem entidade C#)
        builder.HasIndex(ur => ur.UserId)
               .HasDatabaseName("IX_UserRole_UserId");
    }
}
