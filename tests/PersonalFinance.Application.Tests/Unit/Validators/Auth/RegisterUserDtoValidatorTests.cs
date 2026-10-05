using FluentAssertions;
using FluentValidation;
using PersonalFinance.Application.DTOs.Auth;
using PersonalFinance.Application.Tests.Unit.Support;
using Xunit;

namespace PersonalFinance.Application.Tests.Unit.Validators.Auth;

public class RegisterUserDtoValidatorTests
{
    private readonly IValidator<RegisterUserDto> _sut = ValidatorLocator.Get<RegisterUserDto>();

    private static string Str(int n) => new('a', n);
    private static string Email(int total) => new string('a', total - "@x.com".Length) + "@x.com";

    private static RegisterUserDto Dto(string name = "Caique", string email = "caique@x.com", string password = "Senha@123")
        => new(name, email, password);

    [Fact] public void Valid_ShouldPass() => _sut.Validate(Dto()).IsValid.Should().BeTrue();

    [Theory, InlineData(""), InlineData("   "), InlineData(null)]
    public void Name_Empty_ShouldFail(string? v) => _sut.Validate(Dto(name: v!)).Errors.Should().Contain(e => e.PropertyName == "Name");

    [Fact] public void Name_100_ShouldPass() => _sut.Validate(Dto(name: Str(100))).IsValid.Should().BeTrue();
    [Fact] public void Name_101_ShouldFail() => _sut.Validate(Dto(name: Str(101))).Errors.Should().Contain(e => e.PropertyName == "Name");

    [Theory, InlineData(""), InlineData(null), InlineData("nao_e_email"), InlineData("a@"), InlineData("@x.com")]
    public void Email_Invalid_ShouldFail(string? v) => _sut.Validate(Dto(email: v!)).Errors.Should().Contain(e => e.PropertyName == "Email");

    [Fact] public void Email_200_ShouldPass() => _sut.Validate(Dto(email: Email(200))).IsValid.Should().BeTrue();
    [Fact] public void Email_201_ShouldFail() => _sut.Validate(Dto(email: Email(201))).Errors.Should().Contain(e => e.PropertyName == "Email");

    [Theory, InlineData(""), InlineData(null), InlineData("1234567")]
    public void Password_TooShortOrEmpty_ShouldFail(string? v) => _sut.Validate(Dto(password: v!)).Errors.Should().Contain(e => e.PropertyName == "Password");

    [Fact] public void Password_8_ShouldPass() => _sut.Validate(Dto(password: Str(8))).IsValid.Should().BeTrue();
    [Fact] public void Password_128_ShouldPass() => _sut.Validate(Dto(password: Str(128))).IsValid.Should().BeTrue();
    [Fact] public void Password_129_ShouldFail() => _sut.Validate(Dto(password: Str(129))).Errors.Should().Contain(e => e.PropertyName == "Password");
}
