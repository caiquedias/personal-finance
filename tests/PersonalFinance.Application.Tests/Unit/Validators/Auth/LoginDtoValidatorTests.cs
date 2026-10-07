using FluentAssertions;
using FluentValidation;
using PersonalFinance.Application.DTOs.Auth;
using PersonalFinance.Application.Tests.Unit.Support;
using Xunit;

namespace PersonalFinance.Application.Tests.Unit.Validators.Auth;

public class LoginDtoValidatorTests
{
    private readonly IValidator<LoginDto> _sut = ValidatorLocator.Get<LoginDto>();

    private static string Str(int n) => new('a', n);
    private static string Email(int total) => new string('a', total - "@x.com".Length) + "@x.com";

    private static LoginDto Dto(string email = "caique@x.com", string password = "Senha@123") => new(email, password);

    [Fact] public void Valid_ShouldPass() => _sut.Validate(Dto()).IsValid.Should().BeTrue();

    [Theory, InlineData(""), InlineData("   "), InlineData(null)]
    public void Email_Empty_ShouldFail(string? v) => _sut.Validate(Dto(email: v!)).Errors.Should().Contain(e => e.PropertyName == "Email");

    [Fact] public void Email_201_ShouldFail() => _sut.Validate(Dto(email: Email(201))).Errors.Should().Contain(e => e.PropertyName == "Email");

    // Sem checagem de formato: evita vetor de enumeração e mantém o fluxo de "Credenciais inválidas."
    [Fact] public void Email_BadFormat_ShouldPass() => _sut.Validate(Dto(email: "nao_e_email")).IsValid.Should().BeTrue();

    // Senha curta NÃO pode ser rejeitada no login (lockout/throttle contam tentativas com "Errada")
    [Theory, InlineData("Errada"), InlineData("a")]
    public void Password_Short_ShouldPass(string v) => _sut.Validate(Dto(password: v)).IsValid.Should().BeTrue();

    [Theory, InlineData(""), InlineData(null)]
    public void Password_Empty_ShouldFail(string? v) => _sut.Validate(Dto(password: v!)).Errors.Should().Contain(e => e.PropertyName == "Password");

    [Fact] public void Password_128_ShouldPass() => _sut.Validate(Dto(password: Str(128))).IsValid.Should().BeTrue();
    [Fact] public void Password_129_ShouldFail() => _sut.Validate(Dto(password: Str(129))).Errors.Should().Contain(e => e.PropertyName == "Password");
}
