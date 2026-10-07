using FluentAssertions;
using FluentValidation;
using PersonalFinance.Application.DTOs.Financial;
using PersonalFinance.Application.Tests.Unit.Support;
using Xunit;

namespace PersonalFinance.Application.Tests.Unit.Validators.Financial;

public class CreatePeriodDtoValidatorTests
{
    private readonly IValidator<CreatePeriodDto> _sut = ValidatorLocator.Get<CreatePeriodDto>();

    private static CreatePeriodDto Dto(Guid? userId = null, int year = 2026, int month = 5)
        => new(userId ?? Guid.NewGuid(), year, month);

    private bool Fails(CreatePeriodDto dto, string prop) => _sut.Validate(dto).Errors.Any(e => e.PropertyName == prop);

    [Fact] public void Valid_ShouldPass() => _sut.Validate(Dto()).IsValid.Should().BeTrue();

    [Fact] public void UserId_Empty_ShouldFail() => Fails(Dto(Guid.Empty), "UserId").Should().BeTrue();

    [Theory, InlineData(0), InlineData(13), InlineData(-1)]
    public void Month_OutOfRange_ShouldFail(int m) => Fails(Dto(month: m), "Month").Should().BeTrue();

    [Theory, InlineData(1), InlineData(12)]
    public void Month_Boundaries_ShouldPass(int m) => _sut.Validate(Dto(month: m)).IsValid.Should().BeTrue();

    [Theory, InlineData(1899), InlineData(2101), InlineData(0), InlineData(int.MaxValue)]
    public void Year_OutOfRange_ShouldFail(int y) => Fails(Dto(year: y), "Year").Should().BeTrue();

    [Theory, InlineData(1900), InlineData(2100)]
    public void Year_Boundaries_ShouldPass(int y) => _sut.Validate(Dto(year: y)).IsValid.Should().BeTrue();
}
