using FluentAssertions;
using FluentValidation;
using PersonalFinance.Application.DTOs.Financial;
using PersonalFinance.Application.Tests.Unit.Support;
using PersonalFinance.Domain.Enums;
using System.Globalization;
using Xunit;

namespace PersonalFinance.Application.Tests.Unit.Validators.Financial;

public class UpdateExpenseDtoValidatorTests
{
    private readonly IValidator<UpdateExpenseDto> _sut = ValidatorLocator.Get<UpdateExpenseDto>();

    private static string Str(int n) => new('a', n);
    private static decimal D(string s) => decimal.Parse(s, CultureInfo.InvariantCulture);

    private static UpdateExpenseDto Dto(
        Guid? id = null, Guid? userId = null, Guid? categoryId = null,
        SourceType source = SourceType.Parental, FortnightType fortnight = FortnightType.First,
        string description = "Aluguel", decimal amount = 146.52m,
        DateOnly? dueDate = null, string? notes = null, PaymentStatus status = PaymentStatus.Pending)
        => new(id ?? Guid.NewGuid(), userId ?? Guid.NewGuid(), categoryId ?? Guid.NewGuid(),
               source, fortnight, description, amount, dueDate ?? new DateOnly(2026, 5, 10), notes, status);

    private bool Fails(UpdateExpenseDto dto, string prop) => _sut.Validate(dto).Errors.Any(e => e.PropertyName == prop);

    [Fact] public void Valid_ShouldPass() => _sut.Validate(Dto()).IsValid.Should().BeTrue();

    [Fact] public void Id_Empty_ShouldFail() => Fails(Dto(id: Guid.Empty), "Id").Should().BeTrue();
    [Fact] public void UserId_Empty_ShouldFail() => Fails(Dto(userId: Guid.Empty), "UserId").Should().BeTrue();
    [Fact] public void CategoryId_Empty_ShouldFail() => Fails(Dto(categoryId: Guid.Empty), "CategoryId").Should().BeTrue();

    [Fact] public void SourceType_OutOfRange_ShouldFail() => Fails(Dto(source: (SourceType)999), "SourceType").Should().BeTrue();
    [Fact] public void FortnightType_OutOfRange_ShouldFail() => Fails(Dto(fortnight: (FortnightType)999), "FortnightType").Should().BeTrue();
    [Fact] public void Status_OutOfRange_ShouldFail() => Fails(Dto(status: (PaymentStatus)999), "Status").Should().BeTrue();

    [Theory, InlineData(""), InlineData("   "), InlineData(null)]
    public void Description_EmptyOrWhitespace_ShouldFail(string? v) => Fails(Dto(description: v!), "Description").Should().BeTrue();

    [Fact] public void Description_200_ShouldPass() => _sut.Validate(Dto(description: Str(200))).IsValid.Should().BeTrue();
    [Fact] public void Description_201_ShouldFail() => Fails(Dto(description: Str(201)), "Description").Should().BeTrue();

    [Fact] public void Notes_Null_ShouldPass() => _sut.Validate(Dto(notes: null)).IsValid.Should().BeTrue();
    [Fact] public void Notes_500_ShouldPass() => _sut.Validate(Dto(notes: Str(500))).IsValid.Should().BeTrue();
    [Fact] public void Notes_501_ShouldFail() => Fails(Dto(notes: Str(501)), "Notes").Should().BeTrue();

    [Theory, InlineData("0"), InlineData("-1"), InlineData("146.527"), InlineData("1000000000")]
    public void Amount_Invalid_ShouldFail(string v) => Fails(Dto(amount: D(v)), "Amount").Should().BeTrue();

    [Theory, InlineData("0.01"), InlineData("146.52"), InlineData("999999999.99")]
    public void Amount_Valid_ShouldPass(string v) => _sut.Validate(Dto(amount: D(v))).IsValid.Should().BeTrue();

    [Fact] public void DueDate_Default_ShouldFail() => Fails(Dto(dueDate: default(DateOnly)), "DueDate").Should().BeTrue();
}
