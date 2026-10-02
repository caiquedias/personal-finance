using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace PersonalFinance.Api.Tests.Integration
{
    public class AuthControllerTests : ApiIntegrationTestBase
    {
        public AuthControllerTests(TestWebApplicationFactory f) : base(f) { }

        [Fact(DisplayName = "POST /register deve retornar 201 para dados válidos")]
        public async Task Register_WithValidData_ShouldReturn201()
        {
            var r = await Client.PostAsJsonAsync("/api/v1/auth/register", new
            { name = "Caique", email = $"c_{Guid.NewGuid():N}@x.com", password = "Senha@123" });
            r.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        [Fact(DisplayName = "POST /register deve retornar 400 para e-mail inválido")]
        public async Task Register_WithInvalidEmail_ShouldReturn400()
        {
            var r = await Client.PostAsJsonAsync("/api/v1/auth/register", new
            { name = "X", email = "nao_e_email", password = "Senha@123" });
            r.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact(DisplayName = "POST /register deve retornar 400 para e-mail duplicado")]
        public async Task Register_WithDuplicateEmail_ShouldReturn400()
        {
            var email = $"dup_{Guid.NewGuid():N}@x.com";
            await Client.PostAsJsonAsync("/api/v1/auth/register",
                new { name = "A", email, password = "Senha@123" });
            var r = await Client.PostAsJsonAsync("/api/v1/auth/register",
                new { name = "B", email, password = "Senha@123" });
            r.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact(DisplayName = "POST /login deve retornar 200 e token para credenciais válidas")]
        public async Task Login_WithValidCredentials_ShouldReturn200WithToken()
        {
            var email = $"l_{Guid.NewGuid():N}@x.com";
            var created = await Client.PostAsJsonAsync("/api/v1/auth/register",
                new { name = "L", email, password = "Senha@123" });

            var result = await created.Content.ReadAsStringAsync();

            var r = await Client.PostAsJsonAsync("/api/v1/auth/login",
                new { email, password = "Senha@123" });
            r.StatusCode.Should().Be(HttpStatusCode.OK);
            var b = await r.Content.ReadFromJsonAsync<JsonElement>();
            b.GetProperty("token").GetString().Should().NotBeNullOrEmpty();
        }

        [Fact(DisplayName = "POST /login deve retornar 400 para senha incorreta")]
        public async Task Login_WithWrongPassword_ShouldReturn400()
        {
            var email = $"w_{Guid.NewGuid():N}@x.com";
            await Client.PostAsJsonAsync("/api/v1/auth/register",
                new { name = "W", email, password = "Senha@123" });
            var r = await Client.PostAsJsonAsync("/api/v1/auth/login",
                new { email, password = "Errada" });
            r.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact(DisplayName = "POST /login deve recusar senha correta após 5 falhas seguidas (lockout)")]
        public async Task Login_AfterFiveFailures_ShouldRejectCorrectPassword()
        {
            // E-mail único para não bloquear o admin seed usado por outros testes
            var email = $"lock_{Guid.NewGuid():N}@x.com";
            await Client.PostAsJsonAsync("/api/v1/auth/register",
                new { name = "Lock", email, password = "Senha@123" });

            for (var i = 0; i < 5; i++)
                await Client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "Errada" });

            var r = await Client.PostAsJsonAsync("/api/v1/auth/login",
                new { email, password = "Senha@123" });

            r.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await r.Content.ReadAsStringAsync()).Should().Contain("Credenciais inválidas.");
        }

        [Fact(DisplayName = "POST /login com menos de 5 falhas deve continuar aceitando a senha correta")]
        public async Task Login_AfterFourFailures_ShouldStillAcceptCorrectPassword()
        {
            var email = $"four_{Guid.NewGuid():N}@x.com";
            await Client.PostAsJsonAsync("/api/v1/auth/register",
                new { name = "Four", email, password = "Senha@123" });

            for (var i = 0; i < 4; i++)
                await Client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "Errada" });

            var r = await Client.PostAsJsonAsync("/api/v1/auth/login",
                new { email, password = "Senha@123" });

            r.StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }
}
