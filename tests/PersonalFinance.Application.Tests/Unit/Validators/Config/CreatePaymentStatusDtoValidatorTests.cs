using FluentAssertions;
using FluentValidation;
using PersonalFinance.Application.DTOs.Admin;
using PersonalFinance.Application.DTOs.Config;
using PersonalFinance.Application.Tests.Unit.Support;
using Xunit;

namespace PersonalFinance.Application.Tests.Unit.Validators.Config;

public class CreatePaymentStatusDtoValidatorTests
{
    private readonly IValidator<CreatePaymentStatusDto> _sut = ValidatorLocator.Get<CreatePaymentStatusDto>();

    private static string Str(int n) => new('a', n);
    private static CreatePaymentStatusDto Dto(string name = "Parcelado", string? desc = "Desc") => new(name, desc!);

    private bool Fails(CreatePaymentStatusDto dto, string prop) => _sut.Validate(dto).Errors.Any(e => e.PropertyName == prop);

    [Fact] public void Valid_ShouldPass() => _sut.Validate(Dto()).IsValid.Should().BeTrue();

    [Theory, InlineData(""), InlineData("   "), InlineData(null)]
    public void Name_EmptyOrWhitespace_ShouldFail(string? v) => Fails(Dto(name: v!), "Name").Should().BeTrue();

    [Fact] public void Name_50_ShouldPass() => _sut.Validate(Dto(name: Str(50))).IsValid.Should().BeTrue();
    [Fact] public void Name_51_ShouldFail() => Fails(Dto(name: Str(51)), "Name").Should().BeTrue();

    [Fact] public void Description_Null_ShouldPass() => _sut.Validate(Dto(desc: null)).IsValid.Should().BeTrue();
    [Fact] public void Description_200_ShouldPass() => _sut.Validate(Dto(desc: Str(200))).IsValid.Should().BeTrue();
    [Fact] public void Description_201_ShouldFail() => Fails(Dto(desc: Str(201)), "Description").Should().BeTrue();
}
