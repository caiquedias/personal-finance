using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using Xunit;
using static PersonalFinance.Api.Tests.Integration.MfaTestHelper;

namespace PersonalFinance.Api.Tests.Integration;

/// <summary>
/// Feature flag Auth:Mfa:Enforce (#393), default false: com a flag desligada o login segue como hoje,
/// mesmo para usuário com MFA ativo (o frontend atual não quebra). Usa a factory padrão, sem override.
/// </summary>
public class MfaEnforceFlagTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public MfaEnforceFlagTests(TestWebApplicationFactory factory) => _factory = factory;

    [Fact(DisplayName = "Enforce desligado (default): usuário com MFA ativo recebe o token completo no login")]
    public async Task Login_UserWithMfaAndEnforceOff_ShouldReturnFullToken()
    {
        var mfa = await CreateMfaUserAsync(_factory);

        var response = await _factory.CreateClient().PostAsJsonAsync(LoginPath, new { email = mfa.Email, password = mfa.Password });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(response);
        body.GetProperty("token").GetString().Should().NotBeNullOrWhiteSpace();
        (body.TryGetProperty("mfaRequired", out var required) && required.GetBoolean()).Should().BeFalse();
        (body.TryGetProperty("mfaToken", out var mfaToken) && mfaToken.GetString() is not null).Should().BeFalse();
    }

    [Fact(DisplayName = "Resposta de login de usuário sem MFA mantém os campos atuais (token, name, email)")]
    public async Task Login_UserWithoutMfa_ShouldKeepCurrentContract()
    {
        var client = _factory.CreateClient();
        var email = await RegisterUserAsync(client);

        var response = await client.PostAsJsonAsync(LoginPath, new { email, password = Password });

        var body = await ReadJsonAsync(response);
        body.GetProperty("token").GetString().Should().NotBeNullOrWhiteSpace();
        body.GetProperty("name").GetString().Should().Be("Mfa User");
        body.GetProperty("email").GetString().Should().Be(email);
    }
}
