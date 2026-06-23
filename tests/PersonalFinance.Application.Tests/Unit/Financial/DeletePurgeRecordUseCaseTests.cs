using FluentAssertions;
using Moq;
using PersonalFinance.Application.UseCases.Financial.Purge;
using PersonalFinance.Domain.Entities.Financial;
using PersonalFinance.Domain.Exceptions;
using PersonalFinance.Domain.Interfaces.Repositories;
using Xunit;

namespace PersonalFinance.Application.Tests.Unit.Financial;

/// <summary>
/// Testes do DeletePurgeRecordUseCase.
/// Regras: valida ownership via GetByIdAndUserAsync; chama DeleteAsync e CommitAsync no caminho feliz;
/// lança DomainException se registro não encontrado ou de outro usuário.
/// </summary>
public class DeletePurgeRecordUseCaseTests
{
    private readonly Mock<IPurgeRepository>   _purgeRepo = new();
    private readonly Mock<IUnitOfWork>        _uow       = new();
    private readonly DeletePurgeRecordUseCase _sut;

    private static readonly Guid UserId   = Guid.NewGuid();
    private static readonly Guid RecordId = Guid.NewGuid();

    public DeletePurgeRecordUseCaseTests()
    {
        _sut = new DeletePurgeRecordUseCase(_purgeRepo.Object, _uow.Object);
    }

    private static PurgeRecord BuildRecord() =>
        PurgeRecord.Create(
            userId:              UserId,
            periodYear:          2026,
            periodMonth:         6,
            totalIncome:         5000m,
            totalExpense:        3000m,
            incomeCount:         2,
            expenseCount:        10,
            categorySummaryJson: "{}",
            csvFileName:         "expurgo_2026_06.csv");

    // ── Caminho feliz ─────────────────────────────────────────────────────────

    [Fact(DisplayName = "Deve deletar o registro e commitar quando encontrado")]
    public async Task Execute_RecordFound_ShouldDeleteAndCommit()
    {
        var record = BuildRecord();

        _purgeRepo.Setup(r => r.GetByIdAndUserAsync(RecordId, UserId, default))
                  .ReturnsAsync(record);

        await _sut.ExecuteAsync(RecordId, UserId);

        _purgeRepo.Verify(r => r.DeleteAsync(record, default), Times.Once);
        _uow.Verify(u => u.CommitAsync(default), Times.Once);
    }

    // ── Registro não encontrado ───────────────────────────────────────────────

    [Fact(DisplayName = "Deve lançar DomainException quando registro não encontrado")]
    public async Task Execute_RecordNotFound_ShouldThrow()
    {
        _purgeRepo.Setup(r => r.GetByIdAndUserAsync(RecordId, UserId, default))
                  .ReturnsAsync((PurgeRecord?)null);

        var act = () => _sut.ExecuteAsync(RecordId, UserId);

        await act.Should().ThrowAsync<DomainException>();
        _purgeRepo.Verify(r => r.DeleteAsync(It.IsAny<PurgeRecord>(), default), Times.Never);
        _uow.Verify(u => u.CommitAsync(default), Times.Never);
    }

    // ── Ownership — registro de outro usuário ─────────────────────────────────

    [Fact(DisplayName = "Deve lançar DomainException quando registro pertence a outro usuário")]
    public async Task Execute_RecordBelongsToOtherUser_ShouldThrow()
    {
        var otherUserId = Guid.NewGuid();

        // GetByIdAndUserAsync retorna null quando userId não bate — ownership validado no repositório
        _purgeRepo.Setup(r => r.GetByIdAndUserAsync(RecordId, otherUserId, default))
                  .ReturnsAsync((PurgeRecord?)null);

        var act = () => _sut.ExecuteAsync(RecordId, otherUserId);

        await act.Should().ThrowAsync<DomainException>();
        _purgeRepo.Verify(r => r.DeleteAsync(It.IsAny<PurgeRecord>(), default), Times.Never);
        _uow.Verify(u => u.CommitAsync(default), Times.Never);
    }
}
