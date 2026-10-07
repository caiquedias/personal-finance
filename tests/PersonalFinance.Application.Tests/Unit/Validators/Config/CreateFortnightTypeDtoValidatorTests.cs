using FluentAssertions;
using FluentValidation;
using PersonalFinance.Application.DTOs.Admin;
using PersonalFinance.Application.DTOs.Config;
using PersonalFinance.Application.Tests.Unit.Support;
using Xunit;

namespace PersonalFinance.Application.Tests.Unit.Validators.Config;

public class CreateFortnightTypeDtoValidatorTests
{
    private readonly IValidator<CreateFortnightTypeDto> _sut = ValidatorLocator.Get<CreateFortnightTypeDto>();

    private static string Str(int n) => new('a', n);
    private static CreateFortnightTypeDto Dto(string name = "Novo") => new(name);

    private bool Fails(CreateFortnightTypeDto dto, string prop) => _sut.Validate(dto).Errors.Any(e => e.PropertyName == prop);

    [Fact] public void Valid_ShouldPass() => _sut.Validate(Dto()).IsValid.Should().BeTrue();

    [Theory, InlineData(""), InlineData("   "), InlineData(null)]
    public void Name_EmptyOrWhitespace_ShouldFail(string? v) => Fails(Dto(name: v!), "Name").Should().BeTrue();

    [Fact] public void Name_50_ShouldPass() => _sut.Validate(Dto(name: Str(50))).IsValid.Should().BeTrue();
    [Fact] public void Name_51_ShouldFail() => Fails(Dto(name: Str(51)), "Name").Should().BeTrue();

}
