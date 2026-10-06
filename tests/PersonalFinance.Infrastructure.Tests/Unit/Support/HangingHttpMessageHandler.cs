namespace PersonalFinance.Infrastructure.Tests.Unit.Support;

/// <summary>Handler de teste que nunca responde: só termina quando o token de cancelamento (timeout do HttpClient) dispara.</summary>
public sealed class HangingHttpMessageHandler : HttpMessageHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.Infinite, cancellationToken);
        return new HttpResponseMessage();
    }
}
