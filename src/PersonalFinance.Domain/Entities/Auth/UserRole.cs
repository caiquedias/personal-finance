namespace PersonalFinance.Domain.Entities.Auth;

/// <summary>
/// Entidade de junção entre User e Role.
/// Sem EntityBase — tabela de associação simples, sem ciclo de vida de domínio.
/// Gerenciada pela Infrastructure, registrada no DbContext.
/// </summary>
public sealed class UserRole
{
    public Guid     UserId     { get; set; }
    public int      RoleId     { get; set; }

    /// <summary>Data UTC em que o perfil foi atribuído ao usuário.</summary>
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
}
