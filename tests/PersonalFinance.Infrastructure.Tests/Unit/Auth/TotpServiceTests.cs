using FluentAssertions;
using OtpNet;
using PersonalFinance.Infrastructure.Auth;
using Xunit;

namespace PersonalFinance.Infrastructure.Tests.Unit.Auth;

/// <summary>
/// TotpService (#393) — RFC 6238, SHA-1, 6 dígitos, passo de 30s, janela ±1 step.
/// Os códigos esperados são calculados de forma independente com Otp.NET.
/// </summary>
public class TotpServiceTests
{
    private static readonly DateTime Now = new(2026, 3, 10, 12, 0, 10, DateTimeKind.Utc);
    private readonly TotpService _sut = new();

    private static string CodeAt(string base32Secret, DateTime at) =>
        new Totp(Base32Encoding.ToBytes(base32Secret)).ComputeTotp(at);

    private static long StepOf(DateTime at) =>
        new DateTimeOffset(at).ToUnixTimeSeconds() / 30;

    [Fact(DisplayName = "GenerateSecret deve gerar Base32 válido com pelo menos 160 bits")]
    public void GenerateSecret_ShouldBeValidBase32()
    {
        var secret = _sut.GenerateSecret();

        secret.Should().MatchRegex("^[A-Z2-7]+$");
        Base32Encoding.ToBytes(secret).Length.Should().BeGreaterOrEqualTo(20);
    }

    [Fact(DisplayName = "GenerateSecret deve gerar secrets distintos a cada chamada")]
    public void GenerateSecret_ShouldBeRandom()
    {
        var secrets = Enumerable.Range(0, 20).Select(_ => _sut.GenerateSecret()).ToList();

        secrets.Should().OnlyHaveUniqueItems();
    }

    [Fact(DisplayName = "BuildOtpAuthUri deve usar issuer MonkeyBomb, label com e-mail e o secret")]
    public void BuildOtpAuthUri_ShouldContainIssuerLabelAndSecret()
    {
        var secret = _sut.GenerateSecret();

        var uri = _sut.BuildOtpAuthUri(secret, "caique@monkeybomb.com");

        uri.Should().StartWith("otpauth://totp/");
        var parsed = new Uri(uri);
        Uri.UnescapeDataString(parsed.AbsolutePath).Should().Contain("MonkeyBomb").And.Contain("caique@monkeybomb.com");
        parsed.Query.Should().Contain($"secret={secret}").And.Contain("issuer=MonkeyBomb");
    }

    [Fact(DisplayName = "BuildOtpAuthUri deve escapar caracteres especiais do e-mail")]
    public void BuildOtpAuthUri_ShouldEscapeEmail()
    {
        var uri = _sut.BuildOtpAuthUri(_sut.GenerateSecret(), "a+b c@x.com");

        uri.Should().NotContain(" ");
        Uri.UnescapeDataString(new Uri(uri).AbsolutePath).Should().Contain("a+b c@x.com");
    }

    [Fact(DisplayName = "ValidateCode deve aceitar o código do step atual e devolver o step")]
    public void ValidateCode_CurrentStep_ShouldReturnStep()
    {
        var secret = _sut.GenerateSecret();

        var step = _sut.ValidateCode(secret, CodeAt(secret, Now), Now);

        step.Should().Be(StepOf(Now));
    }

    [Theory(DisplayName = "ValidateCode deve aceitar a janela de ±1 step e devolver o step do código")]
    [InlineData(-30)]
    [InlineData(30)]
    public void ValidateCode_AdjacentStep_ShouldBeAcceptedWithItsStep(int offsetSeconds)
    {
        var secret = _sut.GenerateSecret();
        var codeTime = Now.AddSeconds(offsetSeconds);

        var step = _sut.ValidateCode(secret, CodeAt(secret, codeTime), Now);

        step.Should().Be(StepOf(codeTime));
    }

    [Theory(DisplayName = "ValidateCode deve rejeitar códigos fora da janela de ±1 step")]
    [InlineData(-90)]
    [InlineData(90)]
    public void ValidateCode_OutsideWindow_ShouldReturnNull(int offsetSeconds)
    {
        var secret = _sut.GenerateSecret();

        var step = _sut.ValidateCode(secret, CodeAt(secret, Now.AddSeconds(offsetSeconds)), Now);

        step.Should().BeNull();
    }

    [Fact(DisplayName = "ValidateCode deve rejeitar código de outro secret")]
    public void ValidateCode_WrongSecret_ShouldReturnNull()
    {
        var secretA = _sut.GenerateSecret();
        var secretB = _sut.GenerateSecret();

        _sut.ValidateCode(secretB, CodeAt(secretA, Now), Now).Should().BeNull();
    }

    [Theory(DisplayName = "ValidateCode deve rejeitar código vazio, não numérico ou de tamanho errado sem lançar")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abcdef")]
    [InlineData("12345")]
    [InlineData("1234567")]
    [InlineData("12 456")]
    public void ValidateCode_MalformedCode_ShouldReturnNull(string? code)
    {
        var secret = _sut.GenerateSecret();

        _sut.ValidateCode(secret, code!, Now).Should().BeNull();
    }
}
