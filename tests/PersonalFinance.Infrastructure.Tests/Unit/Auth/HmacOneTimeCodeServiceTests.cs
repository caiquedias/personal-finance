using FluentAssertions;
using Microsoft.Extensions.Options;
using PersonalFinance.Application.Options;
using PersonalFinance.Domain.Enums;
using PersonalFinance.Infrastructure.Auth;
using System.Text.RegularExpressions;
using Xunit;

namespace PersonalFinance.Infrastructure.Tests.Unit.Auth;

/// <summary>
/// HmacOneTimeCodeService (#404): código de 6 dígitos (CSPRNG) e HMAC-SHA256 com chave do servidor sobre
/// tokenId|userId|purpose|código. O hash muda se qualquer parte mudar; comparação em tempo fixo.
/// </summary>
public class HmacOneTimeCodeServiceTests
{
    // Base64 de 32 bytes (0x00..0x1F) — só para testes
    private const string KeyA = "AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8=";
    private static readonly string KeyB = Convert.ToBase64String(Enumerable.Repeat((byte)0xAB, 32).ToArray());

    private static readonly Guid TokenId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid UserId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static HmacOneTimeCodeService Build(string? key = KeyA) =>
        new(Options.Create(new UserTokenOptions { HmacKey = key }));

    // ── GenerateCode ──────────────────────────────────────────────────────────

    [Fact(DisplayName = "GenerateCode deve produzir sempre exatamente 6 dígitos ASCII")]
    public void GenerateCode_ShouldAlwaysBeSixDigits()
    {
        var sut = Build();

        for (var i = 0; i < 500; i++)
            Regex.IsMatch(sut.GenerateCode(), @"^[0-9]{6}$").Should().BeTrue();
    }

    [Fact(DisplayName = "GenerateCode não deve repetir sempre o mesmo valor (aleatório)")]
    public void GenerateCode_ShouldVary()
    {
        var sut = Build();

        Enumerable.Range(0, 200).Select(_ => sut.GenerateCode()).Distinct().Count().Should().BeGreaterThan(150);
    }

    // ── ComputeHash ───────────────────────────────────────────────────────────

    [Fact(DisplayName = "ComputeHash é determinístico para as mesmas entradas")]
    public void ComputeHash_ShouldBeDeterministic()
    {
        var sut = Build();

        sut.ComputeHash(TokenId, UserId, UserTokenPurpose.PasswordReset, "123456")
            .Should().Be(sut.ComputeHash(TokenId, UserId, UserTokenPurpose.PasswordReset, "123456"));
    }

    [Fact(DisplayName = "ComputeHash muda se o código mudar")]
    public void ComputeHash_ShouldChangeWithCode()
    {
        var sut = Build();

        sut.ComputeHash(TokenId, UserId, UserTokenPurpose.PasswordReset, "123456")
            .Should().NotBe(sut.ComputeHash(TokenId, UserId, UserTokenPurpose.PasswordReset, "123457"));
    }

    [Fact(DisplayName = "ComputeHash muda se o tokenId mudar")]
    public void ComputeHash_ShouldChangeWithTokenId()
    {
        var sut = Build();

        sut.ComputeHash(TokenId, UserId, UserTokenPurpose.PasswordReset, "123456")
            .Should().NotBe(sut.ComputeHash(Guid.NewGuid(), UserId, UserTokenPurpose.PasswordReset, "123456"));
    }

    [Fact(DisplayName = "ComputeHash muda se o userId mudar")]
    public void ComputeHash_ShouldChangeWithUserId()
    {
        var sut = Build();

        sut.ComputeHash(TokenId, UserId, UserTokenPurpose.PasswordReset, "123456")
            .Should().NotBe(sut.ComputeHash(TokenId, Guid.NewGuid(), UserTokenPurpose.PasswordReset, "123456"));
    }

    [Fact(DisplayName = "ComputeHash muda se o propósito mudar")]
    public void ComputeHash_ShouldChangeWithPurpose()
    {
        var sut = Build();

        sut.ComputeHash(TokenId, UserId, UserTokenPurpose.PasswordReset, "123456")
            .Should().NotBe(sut.ComputeHash(TokenId, UserId, UserTokenPurpose.EmailVerification, "123456"));
    }

    [Fact(DisplayName = "ComputeHash muda com a chave do servidor")]
    public void ComputeHash_ShouldChangeWithKey()
    {
        Build(KeyA).ComputeHash(TokenId, UserId, UserTokenPurpose.PasswordReset, "123456")
            .Should().NotBe(Build(KeyB).ComputeHash(TokenId, UserId, UserTokenPurpose.PasswordReset, "123456"));
    }

    [Fact(DisplayName = "ComputeHash não contém o código em claro e cabe na coluna (<= 128)")]
    public void ComputeHash_ShouldNotContainCodeAndFitColumn()
    {
        var hash = Build().ComputeHash(TokenId, UserId, UserTokenPurpose.PasswordReset, "123456");

        hash.Should().NotBeNullOrWhiteSpace();
        hash.Length.Should().BeLessOrEqualTo(128);
        hash.Should().NotContain("123456");
    }

    // ── Verify ────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "Verify retorna true para o código correto")]
    public void Verify_CorrectCode_ShouldBeTrue()
    {
        var sut = Build();
        var hash = sut.ComputeHash(TokenId, UserId, UserTokenPurpose.PasswordReset, "123456");

        sut.Verify(TokenId, UserId, UserTokenPurpose.PasswordReset, "123456", hash).Should().BeTrue();
    }

    [Fact(DisplayName = "Verify retorna false para código errado")]
    public void Verify_WrongCode_ShouldBeFalse()
    {
        var sut = Build();
        var hash = sut.ComputeHash(TokenId, UserId, UserTokenPurpose.PasswordReset, "123456");

        sut.Verify(TokenId, UserId, UserTokenPurpose.PasswordReset, "123457", hash).Should().BeFalse();
    }

    [Fact(DisplayName = "Verify retorna false se tokenId, userId ou propósito diferirem dos usados na emissão")]
    public void Verify_DifferentContext_ShouldBeFalse()
    {
        var sut = Build();
        var hash = sut.ComputeHash(TokenId, UserId, UserTokenPurpose.PasswordReset, "123456");

        sut.Verify(Guid.NewGuid(), UserId, UserTokenPurpose.PasswordReset, "123456", hash).Should().BeFalse();
        sut.Verify(TokenId, Guid.NewGuid(), UserTokenPurpose.PasswordReset, "123456", hash).Should().BeFalse();
        sut.Verify(TokenId, UserId, UserTokenPurpose.EmailVerification, "123456", hash).Should().BeFalse();
    }

    [Fact(DisplayName = "Verify com hash emitido por outra chave retorna false")]
    public void Verify_HashFromAnotherKey_ShouldBeFalse()
    {
        var hash = Build(KeyB).ComputeHash(TokenId, UserId, UserTokenPurpose.PasswordReset, "123456");

        Build(KeyA).Verify(TokenId, UserId, UserTokenPurpose.PasswordReset, "123456", hash).Should().BeFalse();
    }

    [Theory(DisplayName = "Verify nunca lança para hash/código malformado: retorna false")]
    [InlineData("", "123456")]
    [InlineData("   ", "123456")]
    [InlineData("not-a-valid-hash", "123456")]
    [InlineData("AAAA", "123456")]
    public void Verify_MalformedInput_ShouldBeFalseWithoutThrowing(string storedHash, string code)
    {
        var sut = Build();

        var act = () => sut.Verify(TokenId, UserId, UserTokenPurpose.PasswordReset, code, storedHash);

        act.Should().NotThrow();
        sut.Verify(TokenId, UserId, UserTokenPurpose.PasswordReset, code, storedHash).Should().BeFalse();
    }

    [Theory(DisplayName = "Verify com código vazio ou nulo retorna false")]
    [InlineData("")]
    [InlineData(null)]
    public void Verify_EmptyCode_ShouldBeFalse(string? code)
    {
        var sut = Build();
        var hash = sut.ComputeHash(TokenId, UserId, UserTokenPurpose.PasswordReset, "123456");

        sut.Verify(TokenId, UserId, UserTokenPurpose.PasswordReset, code!, hash).Should().BeFalse();
    }

    // ── Chave ─────────────────────────────────────────────────────────────────

    [Theory(DisplayName = "Chave ausente ou não Base64 deve falhar na construção, sem vazar o valor")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-base64!!")]
    public void Constructor_WithInvalidKey_ShouldThrowWithoutLeakingValue(string? key)
    {
        var act = () => Build(key);

        var ex = act.Should().Throw<Exception>().Which;
        if (!string.IsNullOrEmpty(key))
            ex.ToString().Should().NotContain(key);
    }
}
