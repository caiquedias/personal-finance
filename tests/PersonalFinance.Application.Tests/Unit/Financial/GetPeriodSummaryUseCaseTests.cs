using FluentAssertions;
using Moq;
using PersonalFinance.Application.DTOs.Financial;
using PersonalFinance.Application.Interfaces;
using PersonalFinance.Application.UseCases.Financial.Periods;
using PersonalFinance.Domain.Exceptions;
using PersonalFinance.Domain.Interfaces.Repositories;
using Xunit;

namespace PersonalFinance.Application.Tests.Unit.Financial
{
    public class GetPeriodSummaryUseCaseTests
    {
        private readonly Mock<IPeriodRepository> _periodRepo = new();
        private readonly Mock<IReportRepository> _reportRepo = new();  // Application.Interfaces
        private readonly GetPeriodSummaryUseCase _sut;

        private static readonly Guid UserId = Guid.NewGuid();
        private static readonly Guid PeriodId = Guid.NewGuid();

        public GetPeriodSummaryUseCaseTests() =>
            _sut = new GetPeriodSummaryUseCase(_periodRepo.Object, _reportRepo.Object);

        [Fact(DisplayName = "Deve retornar resumo para período existente")]
        public async Task Execute_WithExistingPeriod_ShouldReturnSummary()
        {
            _periodRepo.Setup(r => r.ExistsByIdAndUserAsync(PeriodId, UserId, default))
                       .ReturnsAsync(true);

            _reportRepo.Setup(r => r.GetPeriodSummaryAsync(PeriodId, UserId, default))
                       .ReturnsAsync(new PeriodSummaryDto(
                           PeriodId: PeriodId,
                           UserId: UserId,
                           Year: 2026,
                           Month: 4,
                           TotalIncome: 8113.15m,
                           TotalExpense: 6440.86m,
                           TotalPaid: 0m,
                           TotalOwed: 6440.86m,
                           TotalFirstFortnight: 3200m,
                           TotalSecondFortnight: 3240.86m,
                           Balance: 1672.29m
                       ));

            var result = await _sut.ExecuteAsync(PeriodId, UserId);

            result.Balance.Should().Be(1672.29m);
            result.TotalIncome.Should().Be(8113.15m);
            result.TotalExpense.Should().Be(6440.86m);
        }

        [Fact(DisplayName = "Deve lançar exceção se período não pertencer ao usuário")]
        public async Task Execute_WithUnauthorizedPeriod_ShouldThrow()
        {
            _periodRepo.Setup(r => r.ExistsByIdAndUserAsync(PeriodId, UserId, default))
                       .ReturnsAsync(false);

            var act = () => _sut.ExecuteAsync(PeriodId, UserId);

            await act.Should().ThrowAsync<DomainException>()
                     .WithMessage("*período*");
        }

        [Fact(DisplayName = "Balance deve ser TotalIncome menos TotalExpense")]
        public async Task Execute_Balance_ShouldBeIncomMinusExpense()
        {
            _periodRepo.Setup(r => r.ExistsByIdAndUserAsync(PeriodId, UserId, default))
                       .ReturnsAsync(true);
            _reportRepo.Setup(r => r.GetPeriodSummaryAsync(PeriodId, UserId, default))
                       .ReturnsAsync(new PeriodSummaryDto(
                           PeriodId, UserId, 2026, 4,
                           TotalIncome: 1000m,
                           TotalExpense: 700m,
                           TotalPaid: 700m,
                           TotalOwed: 0m,
                           TotalFirstFortnight: 400m,
                           TotalSecondFortnight: 300m,
                           Balance: 300m
                       ));

            var result = await _sut.ExecuteAsync(PeriodId, UserId);

            result.Balance.Should().Be(result.TotalIncome - result.TotalExpense);
        }
    }
}
