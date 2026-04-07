using PersonalFinance.Domain.Exceptions;
using System.Net;
using System.Text.Json;

namespace PersonalFinance.Api.Middleware;

/// <summary>
/// Middleware global de tratamento de exceções.
/// DomainException  → 400 Bad Request  (erro de regra de negócio)
/// UnauthorizedAccessException → 401 Unauthorized
/// KeyNotFoundException        → 404 Not Found
/// Qualquer outra              → 500 Internal Server Error (sem expor detalhes em produção)
/// </summary>
public sealed class ExceptionMiddleware(
    RequestDelegate next,
    ILogger<ExceptionMiddleware> logger,
    IHostEnvironment env)
{
    private readonly RequestDelegate _next = next;
    private readonly ILogger<ExceptionMiddleware> _logger = logger;
    private readonly IHostEnvironment _env = env;

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (DomainException ex)
        {
            // Erros de regra de negócio — safe para exibir ao cliente
            _logger.LogWarning("DomainException: {Message}", ex.Message);
            await WriteResponseAsync(context, HttpStatusCode.BadRequest, ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("UnauthorizedAccess: {Message}", ex.Message);
            await WriteResponseAsync(context, HttpStatusCode.Unauthorized, "Acesso não autorizado.");
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning("NotFound: {Message}", ex.Message);
            await WriteResponseAsync(context, HttpStatusCode.NotFound, ex.Message);
        }
        catch (Exception ex)
        {
            // Erro interno — nunca expõe stack trace em produção
            _logger.LogError(ex, "Erro interno não tratado.");

            var detail = _env.IsDevelopment() ? ex.ToString() : "Ocorreu um erro interno.";
            await WriteResponseAsync(context, HttpStatusCode.InternalServerError, detail);
        }
    }

    private static async Task WriteResponseAsync(
        HttpContext context,
        HttpStatusCode statusCode,
        string message)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        var payload = JsonSerializer.Serialize(new
        {
            status = (int)statusCode,
            error = statusCode.ToString(),
            message = message,
            traceId = context.TraceIdentifier
        }, _jsonOptions);

        await context.Response.WriteAsync(payload);
    }

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };
}
