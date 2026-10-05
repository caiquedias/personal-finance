using FluentAssertions;
using FluentValidation;
using PersonalFinance.Application.DTOs.Financial;
using PersonalFinance.Application.Tests.Unit.Support;
using PersonalFinance.Domain.Enums;
using Xunit;

namespace PersonalFinance.Application.Tests.Unit.Validators.Financial;

public class CreateExpensesBatchDtoValidatorTests
{
    private readonly IValidator<CreateExpensesBatchDto> _sut = ValidatorLocator.Get<CreateExpensesBatchDto>();

    private static BatchExpenseItemDto Item(string description = "Aluguel")
        => new(Guid.NewGuid(), SourceType.Parental, FortnightType.First, description, 100m, new DateOnly(2026, 5, 10));

    private static CreateExpensesBatchDto Dto(
        Guid? periodId = null, Guid? userId = null, IReadOnlyList<BatchExpenseItemDto>? items = null)
        => new(periodId ?? Guid.NewGuid(), userId ?? Guid.NewGuid(), items ?? new[] { Item() });

    private bool Fails(CreateExpensesBatchDto dto, string prop) => _sut.Validate(dto).Errors.Any(e => e.PropertyName == prop);

    [Fact] public void Valid_ShouldPass() => _sut.Validate(Dto()).IsValid.Should().BeTrue();

    [Fact] public void PeriodId_Empty_ShouldFail() => Fails(Dto(periodId: Guid.Empty), "PeriodId").Should().BeTrue();
    [Fact] public void UserId_Empty_ShouldFail() => Fails(Dto(userId: Guid.Empty), "UserId").Should().BeTrue();

    [Fact] public void Items_Null_ShouldFail() => Fails(new CreateExpensesBatchDto(Guid.NewGuid(), Guid.NewGuid(), null!), "Items").Should().BeTrue();
    [Fact] public void Items_Empty_ShouldFail() => Fails(Dto(items: Array.Empty<BatchExpenseItemDto>()), "Items").Should().BeTrue();

    [Fact] public void Items_500_ShouldPass() => _sut.Validate(Dto(items: Enumerable.Range(0, 500).Select(_ => Item()).ToList())).IsValid.Should().BeTrue();
    [Fact] public void Items_501_ShouldFail() => Fails(Dto(items: Enumerable.Range(0, 501).Select(_ => Item()).ToList()), "Items").Should().BeTrue();

    [Fact]
    public void Item_Invalid_ShouldFailWithIndexedPropertyName()
        => _sut.Validate(Dto(items: new[] { Item(), Item(description: "") })).Errors
            .Should().Contain(e => e.PropertyName.StartsWith("Items[1]") && e.PropertyName.EndsWith("Description"));
}
