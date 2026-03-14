using FluentAssertions;
using PersonalFinance.Domain.Entities.Shared;
using Xunit;

namespace PersonalFinance.Domain.Tests.Unit.Shared;

/// <summary>
/// Testes da classe abstrata EntityBase.
/// Utiliza um stub concreto (TestEntity) para instanciar a classe abstrata.
/// </summary>
public class EntityBaseTests
{
    // ── Stub ──────────────────────────────────────────────────────────────────
    private sealed class TestEntity : EntityBase { }

    // ── Criação ───────────────────────────────────────────────────────────────

    [Fact(DisplayName = "Deve gerar Id não vazio ao criar entidade")]
    public void Create_ShouldGenerateNonEmptyId()
    {
        var entity = new TestEntity();
        entity.Id.Should().NotBeEmpty();
    }

    [Fact(DisplayName = "Deve gerar Ids únicos para instâncias distintas")]
    public void Create_ShouldGenerateUniqueIds()
    {
        var a = new TestEntity();
        var b = new TestEntity();
        a.Id.Should().NotBe(b.Id);
    }

    [Fact(DisplayName = "CreatedAt deve ser preenchido na criação")]
    public void Create_ShouldSetCreatedAt()
    {
        var before = DateTime.UtcNow;
        var entity = new TestEntity();
        var after  = DateTime.UtcNow;

        entity.CreatedAt.Should().BeOnOrAfter(before)
                                 .And.BeOnOrBefore(after);
    }

    [Fact(DisplayName = "UpdatedAt deve ser igual a CreatedAt na criação")]
    public void Create_UpdatedAtShouldEqualCreatedAt()
    {
        var entity = new TestEntity();
        entity.UpdatedAt.Should().Be(entity.CreatedAt);
    }

    [Fact(DisplayName = "DeletedAt deve ser nulo na criação")]
    public void Create_DeletedAtShouldBeNull()
    {
        var entity = new TestEntity();
        entity.DeletedAt.Should().BeNull();
    }

    [Fact(DisplayName = "IsActive deve ser true na criação")]
    public void Create_IsActiveShouldBeTrue()
    {
        var entity = new TestEntity();
        entity.IsActive.Should().BeTrue();
    }

    // ── Soft Delete ───────────────────────────────────────────────────────────

    [Fact(DisplayName = "SoftDelete deve preencher DeletedAt")]
    public void SoftDelete_ShouldSetDeletedAt()
    {
        var entity = new TestEntity();
        var before = DateTime.UtcNow;
        entity.SoftDelete();

        entity.DeletedAt.Should().NotBeNull()
                                 .And.BeOnOrAfter(before);
    }

    [Fact(DisplayName = "SoftDelete deve desativar IsActive")]
    public void SoftDelete_ShouldSetIsActiveToFalse()
    {
        var entity = new TestEntity();
        entity.SoftDelete();
        entity.IsActive.Should().BeFalse();
    }

    [Fact(DisplayName = "SoftDelete chamado duas vezes não deve alterar DeletedAt original")]
    public void SoftDelete_CalledTwice_ShouldNotOverrideDeletedAt()
    {
        var entity = new TestEntity();
        entity.SoftDelete();
        var firstDeletedAt = entity.DeletedAt;

        entity.SoftDelete();

        entity.DeletedAt.Should().Be(firstDeletedAt);
    }

    [Fact(DisplayName = "IsDeleted deve retornar true após SoftDelete")]
    public void IsDeleted_ShouldReturnTrueAfterSoftDelete()
    {
        var entity = new TestEntity();
        entity.SoftDelete();
        entity.IsDeleted.Should().BeTrue();
    }

    [Fact(DisplayName = "IsDeleted deve retornar false para entidade ativa")]
    public void IsDeleted_ShouldReturnFalseForActiveEntity()
    {
        var entity = new TestEntity();
        entity.IsDeleted.Should().BeFalse();
    }

    // ── Update ────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "SetUpdatedAt deve atualizar UpdatedAt")]
    public void SetUpdatedAt_ShouldUpdateTimestamp()
    {
        var entity = new TestEntity();
        var original = entity.UpdatedAt;

        // Garante diferença de tempo mínima
        Task.Delay(10).Wait();
        entity.SetUpdatedAt();

        entity.UpdatedAt.Should().BeAfter(original);
    }
}
