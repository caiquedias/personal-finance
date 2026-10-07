using FluentAssertions;
using FluentValidation;
using PersonalFinance.Application.DTOs.Admin;
using PersonalFinance.Application.Tests.Unit.Support;
using Xunit;

namespace PersonalFinance.Application.Tests.Unit.Validators.Admin;

public class UpdateUserByAdminDtoValidatorTests
{
    private readonly IValidator<UpdateUserByAdminDto> _sut = ValidatorLocator.Get<UpdateUserByAdminDto>();

    private static string Str(int n) => new('a', n);
    private static string Email(int total) => new string('a', total - "@x.com".Length) + "@x.com";

    private static UpdateUserByAdminDto Dto(Guid? id = null, string name = "Novo") => new(id ?? Guid.NewGuid(), name);

    [Fact] public void Valid_ShouldPass() => _sut.Validate(Dto()).IsValid.Should().BeTrue();

    [Fact] public void UserId_Empty_ShouldFail() => _sut.Validate(Dto(Guid.Empty)).Errors.Should().Contain(e => e.PropertyName == "UserId");

    [Theory, InlineData(""), InlineData("  "), InlineData(null)]
    public void Name_Empty_ShouldFail(string? v) => _sut.Validate(Dto(name: v!)).Errors.Should().Contain(e => e.PropertyName == "Name");

    [Fact] public void Name_100_ShouldPass() => _sut.Validate(Dto(name: Str(100))).IsValid.Should().BeTrue();
    [Fact] public void Name_101_ShouldFail() => _sut.Validate(Dto(name: Str(101))).Errors.Should().Contain(e => e.PropertyName == "Name");
}
