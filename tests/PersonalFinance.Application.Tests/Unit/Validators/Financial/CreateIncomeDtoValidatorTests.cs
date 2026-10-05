using FluentAssertions;
using FluentValidation;
using PersonalFinance.Application.DTOs.Financial;
using PersonalFinance.Application.Tests.Unit.Support;
using PersonalFinance.Domain.Enums;
using System.Globalization;
using Xunit;

namespace PersonalFinance.Application.Tests.Unit.Validators.Financial;

public class CreateIncomeDtoValidatorTests
{
    private readonly IValidator<CreateIncomeDto> _sut = ValidatorLocator.Get<CreateIncomeDto>();

    private static string Str(int n) => new('a', n);
    private static decimal D(string s) => decimal.Parse(s, CultureInfo.InvariantCulture);

    private static CreateIncomeDto Dto(
        Guid? periodId = null, Guid? userId = null, FortnightType fortnight = FortnightType.First,
        string description = "Salario", decimal amount = 146.52m,
        DateOnly? receivedAt = null, string? notes = null)
        => new(periodId ?? Guid.NewGuid(), userId ?? Guid.NewGuid(), fortnight, description, amount,
               receivedAt ?? new DateOnly(2026, 5, 5), notes);

    private bool Fails(CreateIncomeDto dto, string prop) => _sut.Validate(dto).Errors.Any(e => e.PropertyName == prop);

    [Fact] public void Valid_ShouldPass() => _sut.Validate(Dto()).IsValid.Should().BeTrue();

    [Fact] public void PeriodId_Empty_ShouldFail() => Fails(Dto(periodId: Guid.Empty), "PeriodId").Should().BeTrue();
    [Fact] public void UserId_Empty_ShouldFail() => Fails(Dto(userId: Guid.Empty), "UserId").Should().BeTrue();
    [Fact] public void FortnightType_OutOfRange_ShouldFail() => Fails(Dto(fortnight: (FortnightType)999), "FortnightType").Should().BeTrue();

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

    [Fact] public void ReceivedAt_Default_ShouldFail() => Fails(Dto(receivedAt: default(DateOnly)), "ReceivedAt").Should().BeTrue();
    [Fact] public void ReceivedAt_Future_ShouldPass() => _sut.Validate(Dto(receivedAt: DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1)))).IsValid.Should().BeTrue();
}
