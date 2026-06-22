using FluentAssertions;
using Moq;
using PersonalFinance.Application.Interfaces;
using PersonalFinance.Application.UseCases.Financial.Purge;
using PersonalFinance.Domain.Entities.Financial;
using PersonalFinance.Domain.Exceptions;
using PersonalFinance.Domain.Interfaces.Repositories;
using Xunit;

namespace PersonalFinance.Application.Tests.Unit.Financial;

/// <summary>
/// Testes do ExportPeriodUseCase.
/// Regras: validar ownership do período; delegar exportação ao ICsvExportService; retornar resultado do serviço.
/// </summary>
public class ExportPeriodUseCaseTests
{
    private readonly Mock<IPeriodRepository> _periodRepo  = new();
    private readonly Mock<ICsvExportService> _csvService  = new();
    private readonly ExportPeriodUseCase     _sut;

    private static readonly Guid UserId   = Guid.NewGuid();
    private static readonly Guid PeriodId = Guid.NewGuid();

    public ExportPeriodUseCaseTests()
    {
        _sut = new ExportPeriodUseCase(_periodRepo.Object, _csvService.Object);
    }

    // ── Caminho feliz ─────────────────────────────────────────────────────────

    [Fact(DisplayName = "Deve exportar período e retornar o resultado do serviço CSV")]
    public async Task Execute_ValidPeriod_ShouldDelegateAndReturnCsvResult()
    {
        var period    = Period.Create(UserId, 2026, 4);
        var csvResult = "expurgo_2026_04.csv";

        _periodRepo.Setup(r => r.GetByIdAndUserAsync(PeriodId, UserId, default))
                   .ReturnsAsync(period);
        _csvService.Setup(s => s.ExportPeriodAsync(PeriodId, UserId, default))
                   .ReturnsAsync(csvResult);

        var result = await _sut.ExecuteAsync(PeriodId, UserId);

        result.Should().Be(csvResult);
        _csvService.Verify(s => s.ExportPeriodAsync(PeriodId, UserId, default), Times.Once);
    }

    // ── Período não encontrado ────────────────────────────────────────────────

    [Fact(DisplayName = "Deve lançar exceção quando período não encontrado")]
    public async Task Execute_PeriodNotFound_ShouldThrow()
    {
        _periodRepo.Setup(r => r.GetByIdAndUserAsync(PeriodId, UserId, default))
                   .ReturnsAsync((Period?)null);

        var act = () => _sut.ExecuteAsync(PeriodId, UserId);

        await act.Should().ThrowAsync<DomainException>();
        _csvService.Verify(s => s.ExportPeriodAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), default), Times.Never);
    }

    // ── Validação de ownership ────────────────────────────────────────────────

    [Fact(DisplayName = "Não deve chamar CsvExportService se período não pertencer ao usuário")]
    public async Task Execute_PeriodBelongsToOtherUser_ShouldThrow()
    {
        var otherUserId = Guid.NewGuid();

        _periodRepo.Setup(r => r.GetByIdAndUserAsync(PeriodId, otherUserId, default))
                   .ReturnsAsync((Period?)null);

        var act = () => _sut.ExecuteAsync(PeriodId, otherUserId);

        await act.Should().ThrowAsync<DomainException>();
        _csvService.Verify(s => s.ExportPeriodAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), default), Times.Never);
    }

    // ── CancellationToken propagado ───────────────────────────────────────────

    [Fact(DisplayName = "Deve propagar o CancellationToken ao serviço CSV")]
    public async Task Execute_ShouldPropagateCancellationToken()
    {
        var cts    = new CancellationTokenSource();
        var ct     = cts.Token;
        var period = Period.Create(UserId, 2026, 4);

        _periodRepo.Setup(r => r.GetByIdAndUserAsync(PeriodId, UserId, ct))
                   .ReturnsAsync(period);
        _csvService.Setup(s => s.ExportPeriodAsync(PeriodId, UserId, ct))
                   .ReturnsAsync("file.csv");

        await _sut.ExecuteAsync(PeriodId, UserId, ct);

        _csvService.Verify(s => s.ExportPeriodAsync(PeriodId, UserId, ct), Times.Once);
    }
}
