using FluentAssertions;
using FluentValidation;
using PersonalFinance.Application.DTOs.Financial;
using PersonalFinance.Application.Tests.Unit.Support;
using Xunit;

namespace PersonalFinance.Application.Tests.Unit.Validators.Financial;

public class SaveExpenseOrderDtoValidatorTests
{
    private readonly IValidator<SaveExpenseOrderDto> _sut = ValidatorLocator.Get<SaveExpenseOrderDto>();

    private static List<ExpenseOrderItemDto> Items(int n) =>
        Enumerable.Range(0, n).Select(i => new ExpenseOrderItemDto(Guid.NewGuid(), i)).ToList();

    private static SaveExpenseOrderDto Dto(Guid? userId = null, IEnumerable<ExpenseOrderItemDto>? items = null)
        => new(userId ?? Guid.NewGuid(), items ?? Items(2));

    private bool Fails(SaveExpenseOrderDto dto, string prop) => _sut.Validate(dto).Errors.Any(e => e.PropertyName.StartsWith(prop));

    [Fact] public void Valid_ShouldPass() => _sut.Validate(Dto()).IsValid.Should().BeTrue();

    [Fact] public void UserId_Empty_ShouldFail() => Fails(Dto(userId: Guid.Empty), "UserId").Should().BeTrue();
    [Fact] public void Items_Null_ShouldFail() => Fails(new SaveExpenseOrderDto(Guid.NewGuid(), null!), "Items").Should().BeTrue();

    [Fact] public void Items_1000_ShouldPass() => _sut.Validate(Dto(items: Items(1000))).IsValid.Should().BeTrue();
    [Fact] public void Items_1001_ShouldFail() => Fails(Dto(items: Items(1001)), "Items").Should().BeTrue();

    [Fact]
    public void Item_ExpenseIdEmpty_ShouldFail()
        => Fails(Dto(items: new[] { new ExpenseOrderItemDto(Guid.Empty, 0) }), "Items").Should().BeTrue();

    [Fact]
    public void Item_NegativeOrder_ShouldFail()
        => Fails(Dto(items: new[] { new ExpenseOrderItemDto(Guid.NewGuid(), -1) }), "Items").Should().BeTrue();

    [Fact]
    public void Item_OrderZero_ShouldPass()
        => _sut.Validate(Dto(items: new[] { new ExpenseOrderItemDto(Guid.NewGuid(), 0) })).IsValid.Should().BeTrue();
}
