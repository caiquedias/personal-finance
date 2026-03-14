namespace PersonalFinance.Domain.Entities.Shared;

/// <summary>
/// Classe base abstrata para todas as entidades de domínio.
/// Não gera tabela física própria — colunas replicadas via IEntityTypeConfiguration.
/// EF Core aplica HasQueryFilter global filtrando DeletedAt IS NULL.
/// Timestamps sempre em UTC.
/// </summary>
public abstract class EntityBase
{
    public Guid      Id        { get; private set; }
    public DateTime  CreatedAt { get; private set; }
    public DateTime  UpdatedAt { get; private set; }
    public DateTime? DeletedAt { get; private set; }
    public bool      IsActive  { get; private set; }
    public bool      IsDeleted => DeletedAt.HasValue;

    protected EntityBase()
    {
        Id        = Guid.NewGuid();
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
        IsActive  = true;
    }

    /// <summary>
    /// Executa exclusão lógica do registro.
    /// Idempotente: chamadas subsequentes não alteram o DeletedAt original.
    /// </summary>
    public void SoftDelete()
    {
        if (IsDeleted) return;
        DeletedAt = DateTime.UtcNow;
        IsActive  = false;
        SetUpdatedAt();
    }

    /// <summary>
    /// Reativa um registro previamente excluído logicamente.
    /// Limpa DeletedAt e seta IsActive = true.
    /// </summary>
    public void Reactivate()
    {
        DeletedAt = null;
        IsActive  = true;
        SetUpdatedAt();
    }

    /// <summary>
    /// Atualiza o timestamp de última alteração para UTC agora.
    /// Deve ser chamado em todo método de atualização de entidade.
    /// </summary>
    public void SetUpdatedAt()
    {
        UpdatedAt = DateTime.UtcNow;
    }
}
