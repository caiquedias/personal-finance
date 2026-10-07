using FluentAssertions;
using FluentValidation;
using PersonalFinance.Application.DTOs.Auth;
using PersonalFinance.Application.Tests.Unit.Support;
using Xunit;

namespace PersonalFinance.Application.Tests.Unit.Validators.Auth;

/// <summary>Validação do DTO de confirmação de e-mail (e-mail + código de 6 dígitos) — #404.</summary>
public class ConfirmEmailDtoValidatorTests
{
    private readonly IValidator<ConfirmEmailDto> _sut = ValidatorLocator.Get<ConfirmEmailDto>();

    private static string Email(int total) => new string('a', total - "@x.com".Length) + "@x.com";

    private static ConfirmEmailDto Dto(string email = "ana@x.com", string code = "123456") => new(email, code);

    [Fact] public void Valid_ShouldPass() => _sut.Validate(Dto()).IsValid.Should().BeTrue();

    [Theory, InlineData(""), InlineData(null), InlineData("nao_e_email")]
    public void Email_Invalid_ShouldFail(string? v) =>
        _sut.Validate(Dto(email: v!)).Errors.Should().Contain(e => e.PropertyName == "Email");

    [Fact] public void Email_200_ShouldPass() => _sut.Validate(Dto(email: Email(200))).IsValid.Should().BeTrue();

    [Fact] public void Email_201_ShouldFail() =>
        _sut.Validate(Dto(email: Email(201))).Errors.Should().Contain(e => e.PropertyName == "Email");

    [Theory, InlineData(""), InlineData(null), InlineData("12345"), InlineData("1234567"), InlineData("12345a"),
     InlineData("abcdef"), InlineData("12 456"), InlineData("12345-"), InlineData("١٢٣٤٥٦")]
    public void Code_NotExactlySixAsciiDigits_ShouldFail(string? v) =>
        _sut.Validate(Dto(code: v!)).Errors.Should().Contain(e => e.PropertyName == "Code");

    [Theory, InlineData("000000"), InlineData("999999"), InlineData("012345")]
    public void Code_SixDigits_ShouldPass(string v) => _sut.Validate(Dto(code: v)).IsValid.Should().BeTrue();
}
