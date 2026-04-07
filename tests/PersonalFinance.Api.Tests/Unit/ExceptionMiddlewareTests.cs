using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using PersonalFinance.Api.Middleware;
using PersonalFinance.Domain.Exceptions;
using System.Net;
using System.Text.Json;
using Xunit;

namespace PersonalFinance.Api.Tests.Unit;

public class ExceptionMiddlewareTests
{
    private readonly Mock<ILogger<ExceptionMiddleware>> _logger = new();
    private readonly Mock<IHostEnvironment> _env = new();

    private ExceptionMiddleware CreateSut(RequestDelegate next)
        => new(next, _logger.Object, _env.Object);

    private static DefaultHttpContext CreateContext()
    {
        var ctx = new DefaultHttpContext();
        ctx.Response.Body = new MemoryStream();
        return ctx;
    }

    private static async Task<(int statusCode, string body)> InvokeAsync(
        ExceptionMiddleware middleware, DefaultHttpContext ctx)
    {
        await middleware.InvokeAsync(ctx);
        ctx.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(ctx.Response.Body).ReadToEndAsync();
        return (ctx.Response.StatusCode, body);
    }

    [Fact(DisplayName = "DomainException deve retornar 400 com a mensagem")]
    public async Task Invoke_DomainException_ShouldReturn400()
    {
        var middleware = CreateSut(_ => throw new DomainException("Regra violada."));
        var ctx = CreateContext();

        var (status, body) = await InvokeAsync(middleware, ctx);

        status.Should().Be((int)HttpStatusCode.BadRequest);
        body.Should().Contain("Regra violada.");
    }

    [Fact(DisplayName = "UnauthorizedAccessException deve retornar 401")]
    public async Task Invoke_UnauthorizedException_ShouldReturn401()
    {
        var middleware = CreateSut(_ => throw new UnauthorizedAccessException("Não autorizado."));
        var ctx = CreateContext();

        var (status, _) = await InvokeAsync(middleware, ctx);

        status.Should().Be((int)HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "KeyNotFoundException deve retornar 404 com a mensagem")]
    public async Task Invoke_KeyNotFoundException_ShouldReturn404()
    {
        var middleware = CreateSut(_ => throw new KeyNotFoundException("Recurso não encontrado."));
        var ctx = CreateContext();

        var (status, body) = await InvokeAsync(middleware, ctx);
        var json = JsonSerializer.Deserialize<JsonElement>(body);

        status.Should().Be((int)HttpStatusCode.NotFound);
        json.GetProperty("message").GetString().Should().Be("Recurso não encontrado.");
    }

    [Fact(DisplayName = "Exception genérica deve retornar 500")]
    public async Task Invoke_GenericException_ShouldReturn500()
    {
        var middleware = CreateSut(_ => throw new InvalidOperationException("Erro inesperado."));
        var ctx = CreateContext();

        var (status, _) = await InvokeAsync(middleware, ctx);

        status.Should().Be((int)HttpStatusCode.InternalServerError);
    }

    [Fact(DisplayName = "Em produção, 500 não deve expor detalhes internos")]
    public async Task Invoke_InProduction_ShouldNotExposeStackTrace()
    {
        _env.Setup(e => e.EnvironmentName).Returns("Production");
        var middleware = CreateSut(_ => throw new InvalidOperationException("Detalhe interno."));
        var ctx = CreateContext();

        var (_, body) = await InvokeAsync(middleware, ctx);

        body.Should().NotContain("Detalhe interno.");
        body.Should().Contain("erro interno");
    }

    [Fact(DisplayName = "Em desenvolvimento, 500 deve expor detalhes da exceção")]
    public async Task Invoke_InDevelopment_ShouldExposeExceptionDetails()
    {
        _env.Setup(e => e.EnvironmentName).Returns("Development");
        var middleware = CreateSut(_ => throw new InvalidOperationException("Detalhe interno."));
        var ctx = CreateContext();

        var (_, body) = await InvokeAsync(middleware, ctx);

        body.Should().Contain("Detalhe interno.");
    }

    [Fact(DisplayName = "Resposta de erro deve ser JSON válido com campos esperados")]
    public async Task Invoke_ShouldReturnValidJsonWithExpectedFields()
    {
        var middleware = CreateSut(_ => throw new DomainException("Erro de negócio."));
        var ctx = CreateContext();

        var (_, body) = await InvokeAsync(middleware, ctx);

        var json = JsonSerializer.Deserialize<JsonElement>(body);
        json.GetProperty("status").GetInt32().Should().Be(400);
        json.GetProperty("message").GetString().Should().Be("Erro de negócio.");
        json.TryGetProperty("traceId", out _).Should().BeTrue();
    }

    [Fact(DisplayName = "Deve chamar o próximo middleware quando não há exceção")]
    public async Task Invoke_WithNoException_ShouldCallNext()
    {
        var nextCalled = false;
        var middleware = CreateSut(_ => { nextCalled = true; return Task.CompletedTask; });
        var ctx = CreateContext();

        await middleware.InvokeAsync(ctx);

        nextCalled.Should().BeTrue();
        ctx.Response.StatusCode.Should().Be(200);
    }

    [Fact(DisplayName = "Content-Type da resposta de erro deve ser application/json")]
    public async Task Invoke_OnError_ShouldSetJsonContentType()
    {
        var middleware = CreateSut(_ => throw new DomainException("Erro."));
        var ctx = CreateContext();

        await middleware.InvokeAsync(ctx);

        ctx.Response.ContentType.Should().Be("application/json");
    }
}
