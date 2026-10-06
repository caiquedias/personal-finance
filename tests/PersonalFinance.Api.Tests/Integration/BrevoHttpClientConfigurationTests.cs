using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using PersonalFinance.Application.Interfaces;
using Xunit;

namespace PersonalFinance.Api.Tests.Integration;

/// <summary>
/// HttpClient tipado do provedor de e-mail (#404, C1): timeout explícito de 15s (o padrão de 100s deixaria um
/// Brevo lento bloqueando o consumidor único da fila).
/// </summary>
public class BrevoHttpClientConfigurationTests : IDisposable
{
    private readonly TestWebApplicationFactory _factory = new();

    [Fact(DisplayName = "HttpClient do IEmailSender deve ter Timeout de 15 segundos")]
    public void EmailSenderHttpClient_ShouldHaveFifteenSecondTimeout()
    {
        using var scope = _factory.Services.CreateScope();
        var httpFactory = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();

        // Nome do cliente tipado = nome do tipo de serviço registrado em AddHttpClient<IEmailSender, ...>
        var client = httpFactory.CreateClient(nameof(IEmailSender));

        client.Timeout.Should().Be(TimeSpan.FromSeconds(15));
    }

    public void Dispose() => _factory.Dispose();
}
