using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Http.Json;
using PersonalFinance.Domain.Entities.Auth;
using Xunit;
using static PersonalFinance.Api.Tests.Integration.MfaTestHelper;

namespace PersonalFinance.Api.Tests.Integration;

/// <summary>
/// Gestão de MFA (#393): /auth/mfa/setup, /enable e /disable. Todos exigem token completo
/// e funcionam independente da flag Auth:Mfa:Enforce.
/// </summary>
public class MfaControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public MfaControllerTests(TestWebApplicationFactory factory) => _factory = factory;

    /// <summary>Cadastra, loga e devolve um cliente autenticado com token completo (sem MFA).</summary>
    private async Task<(HttpClient Client, string Email, Guid UserId)> NewAuthedUserAsync()
    {
        var anonymous = _factory.CreateClient();
        var email = await RegisterUserAsync(anonymous);
        var login = await ReadJsonAsync(await anonymous.PostAsJsonAsync(LoginPath, new { email, password = Password }));
        var token = login.GetProperty("token").GetString()!;
        var userId = await WithDbAsync(_factory, db => db.Users.Where(u => u.Email == email).Select(u => u.Id).SingleAsync());
        return (WithBearer(_factory, token), email, userId);
    }

    private async Task<string> SetupAsync(HttpClient client)
    {
        var response = await client.PostAsync(SetupPath, null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await ReadJsonAsync(response)).GetProperty("secret").GetString()!;
    }

    // ── Autenticação obrigatória ──────────────────────────────────────────────

    [Theory(DisplayName = "Endpoints de gestão de MFA devem exigir token (401 sem Authorization)")]
    [InlineData(SetupPath)]
    [InlineData(EnablePath)]
    [InlineData(DisablePath)]
    public async Task ManagementEndpoints_WithoutToken_ShouldReturn401(string path)
    {
        var response = await _factory.CreateClient().PostAsJsonAsync(path, new { code = "123456", password = Password });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── Setup ─────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "Setup deve devolver secret Base32 e URI otpauth do MonkeyBomb")]
    public async Task Setup_ShouldReturnSecretAndOtpAuthUri()
    {
        var (client, email, _) = await NewAuthedUserAsync();

        var response = await client.PostAsync(SetupPath, null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(response);
        body.GetProperty("secret").GetString().Should().MatchRegex("^[A-Z2-7]{16,}$");
        var uri = body.GetProperty("otpAuthUri").GetString()!;
        uri.Should().StartWith("otpauth://totp/").And.Contain("MonkeyBomb");
        Uri.UnescapeDataString(uri).Should().Contain(email);
    }

    [Fact(DisplayName = "Setup deve guardar o secret cifrado (nunca em Base32 claro) e sem ativar o MFA")]
    public async Task Setup_ShouldStoreSecretEncryptedAndNotEnable()
    {
        var (client, _, userId) = await NewAuthedUserAsync();

        var secret = await SetupAsync(client);

        var user = await GetUserAsync(_factory, userId);
        user.MfaEnabled.Should().BeFalse();
        user.MfaSecretEncrypted.Should().NotBeNullOrWhiteSpace();
        user.MfaSecretEncrypted.Should().NotBe(secret).And.NotContain(secret);
    }

    [Fact(DisplayName = "Setup repetido antes do enable deve gerar um novo secret pendente")]
    public async Task Setup_Twice_ShouldReplacePendingSecret()
    {
        var (client, _, userId) = await NewAuthedUserAsync();

        var first = await SetupAsync(client);
        var blobAfterFirst = (await GetUserAsync(_factory, userId)).MfaSecretEncrypted;
        var second = await SetupAsync(client);

        second.Should().NotBe(first);
        (await GetUserAsync(_factory, userId)).MfaSecretEncrypted.Should().NotBe(blobAfterFirst);
    }

    [Fact(DisplayName = "Setup com MFA já ativo deve retornar 400 e não sobrescrever o secret")]
    public async Task Setup_WhenMfaAlreadyEnabled_ShouldReturn400AndKeepSecret()
    {
        var mfa = await CreateMfaUserAsync(_factory);
        var token = (await ReadJsonAsync(await _factory.CreateClient().PostAsJsonAsync(
            LoginPath, new { email = mfa.Email, password = mfa.Password }))).GetProperty("token").GetString()!;
        var blobBefore = (await GetUserAsync(_factory, mfa.UserId)).MfaSecretEncrypted;

        var response = await WithBearer(_factory, token).PostAsync(SetupPath, null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await GetUserAsync(_factory, mfa.UserId)).MfaSecretEncrypted.Should().Be(blobBefore);
    }

    // ── Enable ────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "Enable com código correto deve ativar o MFA e devolver os recovery codes uma vez")]
    public async Task Enable_WithValidCode_ShouldEnableAndReturnRecoveryCodes()
    {
        var (client, _, userId) = await NewAuthedUserAsync();
        var secret = await SetupAsync(client);

        var response = await client.PostAsJsonAsync(EnablePath, new { code = ComputeCode(secret) });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var codes = (await ReadJsonAsync(response)).GetProperty("recoveryCodes").EnumerateArray().Select(e => e.GetString()!).ToList();
        codes.Should().HaveCountGreaterOrEqualTo(8).And.HaveCountLessThanOrEqualTo(10).And.OnlyHaveUniqueItems();
        var user = await GetUserAsync(_factory, userId);
        user.MfaEnabled.Should().BeTrue();
        user.MfaEnabledAt.Should().NotBeNull();
    }

    [Fact(DisplayName = "Recovery codes devem ser persistidos apenas como hash")]
    public async Task Enable_ShouldPersistOnlyHashedRecoveryCodes()
    {
        var mfa = await CreateMfaUserAsync(_factory);

        var stored = await WithDbAsync(_factory, db =>
            db.Set<MfaRecoveryCode>().AsNoTracking().Where(c => c.UserId == mfa.UserId).Select(c => c.CodeHash).ToListAsync());

        stored.Should().HaveCount(mfa.RecoveryCodes.Count);
        stored.Should().NotIntersectWith(mfa.RecoveryCodes);
        stored.Should().OnlyContain(h => !string.IsNullOrWhiteSpace(h));
    }

    [Fact(DisplayName = "Enable sem setup prévio deve retornar 400 e não ativar")]
    public async Task Enable_WithoutSetup_ShouldReturn400()
    {
        var (client, _, userId) = await NewAuthedUserAsync();

        var response = await client.PostAsJsonAsync(EnablePath, new { code = "123456" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await GetUserAsync(_factory, userId)).MfaEnabled.Should().BeFalse();
    }

    [Fact(DisplayName = "Enable com código errado deve retornar 400 e manter o MFA desativado")]
    public async Task Enable_WithWrongCode_ShouldReturn400AndNotEnable()
    {
        var (client, _, userId) = await NewAuthedUserAsync();
        await SetupAsync(client);

        var response = await client.PostAsJsonAsync(EnablePath, new { code = "000000" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await GetUserAsync(_factory, userId)).MfaEnabled.Should().BeFalse();
        (await WithDbAsync(_factory, db => db.Set<MfaRecoveryCode>().CountAsync(c => c.UserId == userId))).Should().Be(0);
    }

    [Fact(DisplayName = "Duplo submit do enable: o 2º pedido não deve gerar novos recovery codes")]
    public async Task Enable_DoubleSubmit_ShouldNotRegenerateCodes()
    {
        var (client, _, userId) = await NewAuthedUserAsync();
        var secret = await SetupAsync(client);
        var code = ComputeCode(secret);

        var first = await client.PostAsJsonAsync(EnablePath, new { code });
        var second = await client.PostAsJsonAsync(EnablePath, new { code });

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        second.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var count = await WithDbAsync(_factory, db => db.Set<MfaRecoveryCode>().CountAsync(c => c.UserId == userId));
        count.Should().BeInRange(8, 10);
    }

    // ── Disable ───────────────────────────────────────────────────────────────

    private async Task<(HttpClient Client, MfaUserContext Mfa)> NewMfaUserWithClientAsync()
    {
        var mfa = await CreateMfaUserAsync(_factory);
        var token = (await ReadJsonAsync(await _factory.CreateClient().PostAsJsonAsync(
            LoginPath, new { email = mfa.Email, password = mfa.Password }))).GetProperty("token").GetString()!;
        return (WithBearer(_factory, token), mfa);
    }

    [Fact(DisplayName = "Disable com senha + TOTP deve limpar secret, recovery codes e desativar o MFA")]
    public async Task Disable_WithPasswordAndTotp_ShouldClearEverything()
    {
        var (client, mfa) = await NewMfaUserWithClientAsync();

        var response = await client.PostAsJsonAsync(DisablePath,
            new { password = mfa.Password, code = ComputeCode(mfa.Secret, 30) });

        response.IsSuccessStatusCode.Should().BeTrue();
        var user = await GetUserAsync(_factory, mfa.UserId);
        user.MfaEnabled.Should().BeFalse();
        user.MfaSecretEncrypted.Should().BeNull();
        var activeCodes = await WithDbAsync(_factory, db => db.Set<MfaRecoveryCode>().CountAsync(c => c.UserId == mfa.UserId));
        activeCodes.Should().Be(0);
    }

    [Fact(DisplayName = "Disable com senha + recovery code deve desativar o MFA")]
    public async Task Disable_WithPasswordAndRecoveryCode_ShouldDisable()
    {
        var (client, mfa) = await NewMfaUserWithClientAsync();

        var response = await client.PostAsJsonAsync(DisablePath,
            new { password = mfa.Password, code = mfa.RecoveryCodes[0] });

        response.IsSuccessStatusCode.Should().BeTrue();
        (await GetUserAsync(_factory, mfa.UserId)).MfaEnabled.Should().BeFalse();
    }

    [Fact(DisplayName = "Disable com senha errada deve retornar 400 e manter o MFA ativo")]
    public async Task Disable_WithWrongPassword_ShouldReturn400AndKeepEnabled()
    {
        var (client, mfa) = await NewMfaUserWithClientAsync();

        var response = await client.PostAsJsonAsync(DisablePath,
            new { password = "SenhaErrada@1", code = ComputeCode(mfa.Secret, 30) });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await GetUserAsync(_factory, mfa.UserId)).MfaEnabled.Should().BeTrue();
    }

    [Fact(DisplayName = "Disable com senha correta e código inválido deve retornar 400 e manter o MFA ativo")]
    public async Task Disable_WithInvalidCode_ShouldReturn400AndKeepEnabled()
    {
        var (client, mfa) = await NewMfaUserWithClientAsync();

        var response = await client.PostAsJsonAsync(DisablePath, new { password = mfa.Password, code = "000000" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var user = await GetUserAsync(_factory, mfa.UserId);
        user.MfaEnabled.Should().BeTrue();
        user.MfaSecretEncrypted.Should().NotBeNull();
    }

    [Fact(DisplayName = "Disable só com senha (sem código) deve retornar 400 e manter o MFA ativo")]
    public async Task Disable_WithoutCode_ShouldReturn400()
    {
        var (client, mfa) = await NewMfaUserWithClientAsync();

        var response = await client.PostAsJsonAsync(DisablePath, new { password = mfa.Password });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await GetUserAsync(_factory, mfa.UserId)).MfaEnabled.Should().BeTrue();
    }

    [Fact(DisplayName = "Após desativar, o usuário pode refazer o setup")]
    public async Task Disable_ThenSetupAgain_ShouldWork()
    {
        var (client, mfa) = await NewMfaUserWithClientAsync();
        await client.PostAsJsonAsync(DisablePath, new { password = mfa.Password, code = ComputeCode(mfa.Secret, 30) });

        var response = await client.PostAsync(SetupPath, null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
