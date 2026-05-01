using FluentAssertions;
using Moq;
using PersonalFinance.Application.UseCases.Financial.Periods;
using PersonalFinance.Domain.Entities.Financial;
using PersonalFinance.Domain.Exceptions;
using PersonalFinance.Domain.Interfaces.Repositories;
using Xunit;

namespace PersonalFinance.Application.Tests.Unit.Financial;

public class TogglePeriodActiveUseCaseTests
{
    private readonly Mock<IPeriodRepository> _periodRepo = new();
    private readonly Mock<IUnitOfWork>       _uow        = new();
    private readonly TogglePeriodActiveUseCase _sut;

    private static readonly Guid UserId   = Guid.NewGuid();
    private static readonly Guid PeriodId = Guid.NewGuid();

    public TogglePeriodActiveUseCaseTests()
    {
        _sut = new TogglePeriodActiveUseCase(_periodRepo.Object, _uow.Object);
    }

    [Fact(DisplayName = "Deve desativar período ativo")]
    public async Task Execute_ActivePeriod_ShouldDeactivate()
    {
        var period = Period.Create(UserId, 2026, 4);
        _periodRepo.Setup(r => r.GetByIdAndUserAsync(PeriodId, UserId, default))
                   .ReturnsAsync(period);

        await _sut.ExecuteAsync(PeriodId, UserId);

        period.IsActive.Should().BeFalse();
        period.IsDeleted.Should().BeFalse();
        _uow.Verify(u => u.CommitAsync(default), Times.Once);
    }

    [Fact(DisplayName = "Deve reativar período inativo")]
    public async Task Execute_InactivePeriod_ShouldReactivate()
    {
        var period = Period.Create(UserId, 2026, 4);
        period.Deactivate();
        _periodRepo.Setup(r => r.GetByIdAndUserAsync(PeriodId, UserId, default))
                   .ReturnsAsync(period);

        await _sut.ExecuteAsync(PeriodId, UserId);

        period.IsActive.Should().BeTrue();
        period.IsDeleted.Should().BeFalse();
        _uow.Verify(u => u.CommitAsync(default), Times.Once);
    }

    [Fact(DisplayName = "Deve lançar exceção para período não encontrado")]
    public async Task Execute_NotFound_ShouldThrow()
    {
        _periodRepo.Setup(r => r.GetByIdAndUserAsync(PeriodId, UserId, default))
                   .ReturnsAsync((Period?)null);

        var act = () => _sut.ExecuteAsync(PeriodId, UserId);

        await act.Should().ThrowAsync<DomainException>()
                 .WithMessage("*Período não encontrado*");
        _uow.Verify(u => u.CommitAsync(default), Times.Never);
    }

    [Fact(DisplayName = "Deve lançar exceção para período de outro usuário")]
    public async Task Execute_WrongUser_ShouldThrow()
    {
        var otherUserId = Guid.NewGuid();
        _periodRepo.Setup(r => r.GetByIdAndUserAsync(PeriodId, otherUserId, default))
                   .ReturnsAsync((Period?)null);

        var act = () => _sut.ExecuteAsync(PeriodId, otherUserId);

        await act.Should().ThrowAsync<DomainException>();
        _uow.Verify(u => u.CommitAsync(default), Times.Never);
    }
}
