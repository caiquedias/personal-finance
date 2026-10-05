using FluentAssertions;
using FluentValidation;
using PersonalFinance.Application.DTOs.Admin;
using PersonalFinance.Application.Tests.Unit.Support;
using Xunit;

namespace PersonalFinance.Application.Tests.Unit.Validators.Admin;

public class ResetPasswordDtoValidatorTests
{
    private readonly IValidator<ResetPasswordDto> _sut = ValidatorLocator.Get<ResetPasswordDto>();

    private static string Str(int n) => new('a', n);
    private static string Email(int total) => new string('a', total - "@x.com".Length) + "@x.com";

    private static ResetPasswordDto Dto(Guid? id = null, string pwd = "NovaSenha@123") => new(id ?? Guid.NewGuid(), pwd);

    [Fact] public void Valid_ShouldPass() => _sut.Validate(Dto()).IsValid.Should().BeTrue();

    [Fact] public void UserId_Empty_ShouldFail() => _sut.Validate(Dto(Guid.Empty)).Errors.Should().Contain(e => e.PropertyName == "UserId");

    [Theory, InlineData(""), InlineData(null), InlineData("1234567")]
    public void NewPassword_TooShortOrEmpty_ShouldFail(string? v) => _sut.Validate(Dto(pwd: v!)).Errors.Should().Contain(e => e.PropertyName == "NewPassword");

    [Fact] public void NewPassword_8_ShouldPass() => _sut.Validate(Dto(pwd: Str(8))).IsValid.Should().BeTrue();
    [Fact] public void NewPassword_128_ShouldPass() => _sut.Validate(Dto(pwd: Str(128))).IsValid.Should().BeTrue();
    [Fact] public void NewPassword_129_ShouldFail() => _sut.Validate(Dto(pwd: Str(129))).Errors.Should().Contain(e => e.PropertyName == "NewPassword");
}
