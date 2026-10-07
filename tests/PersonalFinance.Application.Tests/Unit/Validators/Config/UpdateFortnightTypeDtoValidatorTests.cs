using FluentAssertions;
using FluentValidation;
using PersonalFinance.Application.DTOs.Admin;
using PersonalFinance.Application.DTOs.Config;
using PersonalFinance.Application.Tests.Unit.Support;
using Xunit;

namespace PersonalFinance.Application.Tests.Unit.Validators.Config;

public class UpdateFortnightTypeDtoValidatorTests
{
    private readonly IValidator<UpdateFortnightTypeDto> _sut = ValidatorLocator.Get<UpdateFortnightTypeDto>();

    private static string Str(int n) => new('a', n);
    private static UpdateFortnightTypeDto Dto(string name = "Novo", int id = 3) => new(id, name);

    private bool Fails(UpdateFortnightTypeDto dto, string prop) => _sut.Validate(dto).Errors.Any(e => e.PropertyName == prop);

    [Fact] public void Valid_ShouldPass() => _sut.Validate(Dto()).IsValid.Should().BeTrue();

    [Theory, InlineData(""), InlineData("   "), InlineData(null)]
    public void Name_EmptyOrWhitespace_ShouldFail(string? v) => Fails(Dto(name: v!), "Name").Should().BeTrue();

    [Fact] public void Name_50_ShouldPass() => _sut.Validate(Dto(name: Str(50))).IsValid.Should().BeTrue();
    [Fact] public void Name_51_ShouldFail() => Fails(Dto(name: Str(51)), "Name").Should().BeTrue();

    [Theory, InlineData(0), InlineData(-1)]
    public void Id_NotPositive_ShouldFail(int id) => Fails(Dto(id: id), "Id").Should().BeTrue();
}
