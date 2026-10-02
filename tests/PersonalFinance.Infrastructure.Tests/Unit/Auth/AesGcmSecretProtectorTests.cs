using System.Security.Cryptography;
using FluentAssertions;
using Microsoft.Extensions.Options;
using PersonalFinance.Application.Options;
using PersonalFinance.Infrastructure.Auth;
using Xunit;

namespace PersonalFinance.Infrastructure.Tests.Unit.Auth;

/// <summary>
/// AesGcmSecretProtector (#393) — AES-256-GCM, chave em Auth:Mfa:EncryptionKey (Base64 de 32 bytes),
/// nonce aleatório por registro, formato "nonce|cipher|tag" (cada parte em Base64).
/// </summary>
public class AesGcmSecretProtectorTests
{
    private const string Plain = "JBSWY3DPEHPK3PXPJBSWY3DPEHPK3PXP";

    private static string NewKey() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    private static AesGcmSecretProtector Create(string? key = null) =>
        new(Options.Create(new MfaOptions { EncryptionKey = key ?? NewKey() }));

    [Fact(DisplayName = "Protect/Unprotect deve fazer round-trip do secret")]
    public void RoundTrip_ShouldReturnOriginal()
    {
        var sut = Create();

        sut.Unprotect(sut.Protect(Plain)).Should().Be(Plain);
    }

    [Fact(DisplayName = "O valor protegido não deve conter o secret em claro")]
    public void Protect_ShouldNotLeakPlaintext()
    {
        var protectedValue = Create().Protect(Plain);

        protectedValue.Should().NotBe(Plain).And.NotContain(Plain);
    }

    [Fact(DisplayName = "Formato deve ser nonce|cipher|tag em Base64 (nonce 12 bytes, tag 16 bytes)")]
    public void Protect_ShouldUseNonceCipherTagFormat()
    {
        var parts = Create().Protect(Plain).Split('|');

        parts.Should().HaveCount(3);
        Convert.FromBase64String(parts[0]).Should().HaveCount(12);
        Convert.FromBase64String(parts[1]).Should().HaveCount(Plain.Length);
        Convert.FromBase64String(parts[2]).Should().HaveCount(16);
    }

    [Fact(DisplayName = "Nonce deve ser aleatório: duas proteções do mesmo secret diferem")]
    public void Protect_ShouldUseRandomNonce()
    {
        var sut = Create();

        var a = sut.Protect(Plain);
        var b = sut.Protect(Plain);

        a.Should().NotBe(b);
        sut.Unprotect(a).Should().Be(Plain);
        sut.Unprotect(b).Should().Be(Plain);
    }

    [Fact(DisplayName = "Valor adulterado deve falhar na autenticação (GCM)")]
    public void Unprotect_TamperedCipher_ShouldThrow()
    {
        var sut = Create();
        var parts = sut.Protect(Plain).Split('|');
        var cipher = Convert.FromBase64String(parts[1]);
        cipher[0] ^= 0xFF;
        var tampered = $"{parts[0]}|{Convert.ToBase64String(cipher)}|{parts[2]}";

        var act = () => sut.Unprotect(tampered);

        act.Should().Throw<CryptographicException>();
    }

    [Fact(DisplayName = "Chave diferente não deve conseguir decifrar")]
    public void Unprotect_WithDifferentKey_ShouldThrow()
    {
        var protectedValue = Create().Protect(Plain);

        var act = () => Create().Unprotect(protectedValue);

        act.Should().Throw<CryptographicException>();
    }

    [Theory(DisplayName = "Formato inválido deve lançar exceção")]
    [InlineData("")]
    [InlineData("apenas-uma-parte")]
    [InlineData("a|b")]
    public void Unprotect_MalformedValue_ShouldThrow(string value)
    {
        var act = () => Create().Unprotect(value);

        act.Should().Throw<Exception>();
    }

    [Theory(DisplayName = "Chave ausente, não Base64 ou de tamanho diferente de 32 bytes deve ser rejeitada na criação")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-base64!!")]
    [InlineData("c2hvcnQ=")]
    public void Constructor_WithInvalidKey_ShouldThrow(string? key)
    {
        var act = () => new AesGcmSecretProtector(Options.Create(new MfaOptions { EncryptionKey = key }));

        act.Should().Throw<Exception>();
    }
}
