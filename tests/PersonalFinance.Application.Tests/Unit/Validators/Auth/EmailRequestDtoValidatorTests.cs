using FluentAssertions;
using FluentValidation;
using PersonalFinance.Application.DTOs.Auth;
using PersonalFinance.Application.Tests.Unit.Support;
using Xunit;

namespace PersonalFinance.Application.Tests.Unit.Validators.Auth;

/// <summary>Validação do DTO de e-mail (forgot / resend) — #404.</summary>
public class EmailRequestDtoValidatorTests
{
    private readonly IValidator<EmailRequestDto> _sut = ValidatorLocator.Get<EmailRequestDto>();

    private static string Email(int total) => new string('a', total - "@x.com".Length) + "@x.com";

    [Fact] public void Valid_ShouldPass() => _sut.Validate(new EmailRequestDto("ana@x.com")).IsValid.Should().BeTrue();

    [Theory, InlineData(""), InlineData("   "), InlineData(null), InlineData("nao_e_email"), InlineData("a@"), InlineData("@x.com")]
    public void Email_Invalid_ShouldFail(string? v) =>
        _sut.Validate(new EmailRequestDto(v!)).Errors.Should().Contain(e => e.PropertyName == "Email");

    [Fact] public void Email_200_ShouldPass() => _sut.Validate(new EmailRequestDto(Email(200))).IsValid.Should().BeTrue();

    [Fact] public void Email_201_ShouldFail() =>
        _sut.Validate(new EmailRequestDto(Email(201))).Errors.Should().Contain(e => e.PropertyName == "Email");
}
