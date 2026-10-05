using FluentAssertions;
using FluentValidation;
using PersonalFinance.Application.DTOs.Financial;
using PersonalFinance.Application.Tests.Unit.Support;
using Xunit;

namespace PersonalFinance.Application.Tests.Unit.Validators.Financial;

public class ReplicateExpensesDtoValidatorTests
{
    private readonly IValidator<ReplicateExpensesDto> _sut = ValidatorLocator.Get<ReplicateExpensesDto>();

    private static List<Guid> Ids(int n) => Enumerable.Range(0, n).Select(_ => Guid.NewGuid()).ToList();

    private static ReplicateExpensesDto Dto(Guid? userId = null, Guid? target = null, IReadOnlyList<Guid>? ids = null)
        => new(userId ?? Guid.NewGuid(), target ?? Guid.NewGuid(), ids ?? Ids(2));

    private bool Fails(ReplicateExpensesDto dto, string prop) => _sut.Validate(dto).Errors.Any(e => e.PropertyName.StartsWith(prop));

    [Fact] public void Valid_ShouldPass() => _sut.Validate(Dto()).IsValid.Should().BeTrue();

    [Fact] public void UserId_Empty_ShouldFail() => Fails(Dto(userId: Guid.Empty), "UserId").Should().BeTrue();
    [Fact] public void TargetPeriodId_Empty_ShouldFail() => Fails(Dto(target: Guid.Empty), "TargetPeriodId").Should().BeTrue();

    [Fact] public void ExpenseIds_Null_ShouldFail() => Fails(new ReplicateExpensesDto(Guid.NewGuid(), Guid.NewGuid(), null!), "ExpenseIds").Should().BeTrue();
    [Fact] public void ExpenseIds_Empty_ShouldFail() => Fails(Dto(ids: new List<Guid>()), "ExpenseIds").Should().BeTrue();
    [Fact] public void ExpenseIds_WithGuidEmpty_ShouldFail() => Fails(Dto(ids: new List<Guid> { Guid.NewGuid(), Guid.Empty }), "ExpenseIds").Should().BeTrue();

    [Fact] public void ExpenseIds_500_ShouldPass() => _sut.Validate(Dto(ids: Ids(500))).IsValid.Should().BeTrue();
    [Fact] public void ExpenseIds_501_ShouldFail() => Fails(Dto(ids: Ids(501)), "ExpenseIds").Should().BeTrue();
}
