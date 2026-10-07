using System.Net;

namespace PersonalFinance.Infrastructure.Tests.Unit.Support;

/// <summary>Handler de teste: captura a requisição (snapshot) e devolve a resposta configurada.</summary>
public sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly HttpStatusCode _status;
    private readonly string _responseBody;

    public StubHttpMessageHandler(HttpStatusCode status = HttpStatusCode.Created, string responseBody = "{}")
    {
        _status = status;
        _responseBody = responseBody;
    }

    public int CallCount { get; private set; }
    public HttpMethod? Method { get; private set; }
    public Uri? RequestUri { get; private set; }
    public Dictionary<string, string> Headers { get; } = new(StringComparer.OrdinalIgnoreCase);
    public string? RequestBody { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        CallCount++;
        Method = request.Method;
        RequestUri = request.RequestUri;
        foreach (var header in request.Headers)
            Headers[header.Key] = string.Join(",", header.Value);
        RequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);

        return new HttpResponseMessage(_status) { Content = new StringContent(_responseBody) };
    }
}
