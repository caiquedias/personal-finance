using FluentAssertions;
using FluentValidation;
using PersonalFinance.Application.DTOs.Admin;
using PersonalFinance.Application.DTOs.Config;
using PersonalFinance.Application.Tests.Unit.Support;
using Xunit;

namespace PersonalFinance.Application.Tests.Unit.Validators.Config;

public class UpdateCategoryDtoValidatorTests
{
    private readonly IValidator<UpdateCategoryDto> _sut = ValidatorLocator.Get<UpdateCategoryDto>();

    private static string Str(int n) => new('a', n);
    private static UpdateCategoryDto Dto(string name = "Moradia", string color = "#1E4D2B", string? icon = null, Guid? id = null, Guid? userId = null)
        => new(id ?? Guid.NewGuid(), userId ?? Guid.NewGuid(), name, color, icon);

    private bool Fails(UpdateCategoryDto dto, string prop) => _sut.Validate(dto).Errors.Any(e => e.PropertyName == prop);

    [Fact] public void Valid_ShouldPass() => _sut.Validate(Dto()).IsValid.Should().BeTrue();

    [Theory, InlineData(""), InlineData("   "), InlineData(null)]
    public void Name_EmptyOrWhitespace_ShouldFail(string? v) => Fails(Dto(name: v!), "Name").Should().BeTrue();

    [Fact] public void Name_100_ShouldPass() => _sut.Validate(Dto(name: Str(100))).IsValid.Should().BeTrue();
    [Fact] public void Name_101_ShouldFail() => Fails(Dto(name: Str(101)), "Name").Should().BeTrue();

    [Theory, InlineData(""), InlineData("   "), InlineData(null)]
    public void Color_EmptyOrWhitespace_ShouldFail(string? v) => Fails(Dto(color: v!), "Color").Should().BeTrue();

    [Theory, InlineData("1E4D2B"), InlineData("#FFF"), InlineData("#GGGGGG"), InlineData("#1E4D2B0"), InlineData("#1E4D2")]
    public void Color_InvalidFormat_ShouldFail(string v) => Fails(Dto(color: v), "Color").Should().BeTrue();

    [Theory, InlineData("#1E4D2B"), InlineData("#abcdef"), InlineData("#ABCDEF")]
    public void Color_Valid_ShouldPass(string v) => _sut.Validate(Dto(color: v)).IsValid.Should().BeTrue();

    [Fact] public void Icon_Null_ShouldPass() => _sut.Validate(Dto(icon: null)).IsValid.Should().BeTrue();
    [Fact] public void Icon_50_ShouldPass() => _sut.Validate(Dto(icon: Str(50))).IsValid.Should().BeTrue();
    [Fact] public void Icon_51_ShouldFail() => Fails(Dto(icon: Str(51)), "Icon").Should().BeTrue();

    [Fact] public void Id_Empty_ShouldFail() => Fails(Dto(id: Guid.Empty), "Id").Should().BeTrue();
    [Fact] public void UserId_Empty_ShouldFail() => Fails(Dto(userId: Guid.Empty), "UserId").Should().BeTrue();
}
