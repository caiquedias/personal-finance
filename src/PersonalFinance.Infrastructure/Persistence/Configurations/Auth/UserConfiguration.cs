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

        builder.Property(u => u.FailedLoginCount)
               .HasColumnName("FailedLoginCount")
               .HasColumnType("int")
               .HasDefaultValue(0)
               .IsRequired();

        builder.Property(u => u.LockedUntil)
               .HasColumnName("LockedUntil")
               .HasColumnType("datetime2(7)");

        // Concorrência otimista: evita perda de incremento do contador em logins simultâneos
        builder.Property(u => u.RowVersion)
               .HasColumnName("RowVersion")
               .IsRowVersion();

        // MFA/TOTP (#393): default false mantém os usuários existentes sem MFA
        builder.Property(u => u.MfaEnabled)
               .HasColumnName("MfaEnabled")
               .HasColumnType("bit")
               .HasDefaultValue(false)
               .IsRequired();

        // Blob cifrado "nonce|cipher|tag" em Base64 — nunca o secret em claro
        builder.Property(u => u.MfaSecretEncrypted)
               .HasColumnName("MfaSecretEncrypted")
               .HasColumnType("nvarchar(256)")
               .HasMaxLength(256);

        builder.Property(u => u.MfaEnabledAt)
               .HasColumnName("MfaEnabledAt")
               .HasColumnType("datetime2(7)");

        builder.Property(u => u.LastUsedTotpStep)
               .HasColumnName("LastUsedTotpStep")
               .HasColumnType("bigint");

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
