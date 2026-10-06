using FluentAssertions;
using FluentValidation;
using PersonalFinance.Application.DTOs.Auth;
using PersonalFinance.Application.Tests.Unit.Support;
using Xunit;

namespace PersonalFinance.Application.Tests.Unit.Validators.Auth;

/// <summary>
/// Validação do DTO de conclusão do reset (e-mail, código de 6 dígitos, nova senha mín 8 / máx 128) — #404.
/// A regra de senha é a mesma do cadastro.
/// </summary>
public class CompletePasswordResetDtoValidatorTests
{
    private readonly IValidator<CompletePasswordResetDto> _sut = ValidatorLocator.Get<CompletePasswordResetDto>();

    private static string Str(int n) => new('a', n);
    private static string Email(int total) => new string('a', total - "@x.com".Length) + "@x.com";

    private static CompletePasswordResetDto Dto(
        string email = "ana@x.com", string code = "123456", string newPassword = "NovaSenha@456")
        => new(email, code, newPassword);

    [Fact] public void Valid_ShouldPass() => _sut.Validate(Dto()).IsValid.Should().BeTrue();

    [Theory, InlineData(""), InlineData(null), InlineData("nao_e_email")]
    public void Email_Invalid_ShouldFail(string? v) =>
        _sut.Validate(Dto(email: v!)).Errors.Should().Contain(e => e.PropertyName == "Email");

    [Fact] public void Email_200_ShouldPass() => _sut.Validate(Dto(email: Email(200))).IsValid.Should().BeTrue();

    [Fact] public void Email_201_ShouldFail() =>
        _sut.Validate(Dto(email: Email(201))).Errors.Should().Contain(e => e.PropertyName == "Email");

    [Theory, InlineData(""), InlineData(null), InlineData("12345"), InlineData("1234567"), InlineData("12345a"), InlineData("12 456")]
    public void Code_NotExactlySixDigits_ShouldFail(string? v) =>
        _sut.Validate(Dto(code: v!)).Errors.Should().Contain(e => e.PropertyName == "Code");

    [Theory, InlineData(""), InlineData(null), InlineData("1234567")]
    public void NewPassword_TooShortOrEmpty_ShouldFail(string? v) =>
        _sut.Validate(Dto(newPassword: v!)).Errors.Should().Contain(e => e.PropertyName == "NewPassword");

    [Fact] public void NewPassword_8_ShouldPass() => _sut.Validate(Dto(newPassword: Str(8))).IsValid.Should().BeTrue();
    [Fact] public void NewPassword_128_ShouldPass() => _sut.Validate(Dto(newPassword: Str(128))).IsValid.Should().BeTrue();

    [Fact] public void NewPassword_129_ShouldFail() =>
        _sut.Validate(Dto(newPassword: Str(129))).Errors.Should().Contain(e => e.PropertyName == "NewPassword");
}
