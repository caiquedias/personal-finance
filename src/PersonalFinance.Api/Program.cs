using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using PersonalFinance.Api.Converters;
using PersonalFinance.Api.Extensions;
using PersonalFinance.Api.Middleware;
using PersonalFinance.Infrastructure.Extensions;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// ── Infrastructure (DbContext, repositórios, Argon2id, JWT service) ───────────
builder.Services.AddInfrastructure(builder.Configuration);

// ── Application use cases ─────────────────────────────────────────────────────
builder.Services.AddApplicationUseCases(builder.Configuration);

// ── Controllers ───────────────────────────────────────────────────────────────
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new FlexibleEnumConverterFactory()));

// ── JWT Authentication ────────────────────────────────────────────────────────
var jwtSection = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSection["SecretKey"]
    ?? throw new InvalidOperationException("JwtSettings:SecretKey não configurado.");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
        ValidateIssuer = true,
        ValidIssuer = jwtSection["Issuer"],
        ValidateAudience = true,
        ValidAudience = jwtSection["Audience"],
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero,
    };
});

builder.Services.AddAuthorization();

// ── Swagger / OpenAPI ─────────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Personal Finance API",
        Version = "v1",
        Description = "API de gestão financeira pessoal — MonkeyBomb",
        Contact = new OpenApiContact
        {
            Name = "Caique Dias",
            Email = "caique@monkeybomb.com"
        }
    });

    // Adiciona suporte a JWT no Swagger UI
    var jwtScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Informe o token JWT: Bearer {token}",
        Reference = new OpenApiReference
        {
            Id = JwtBearerDefaults.AuthenticationScheme,
            Type = ReferenceType.SecurityScheme
        }
    };

    options.AddSecurityDefinition(JwtBearerDefaults.AuthenticationScheme, jwtScheme);
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { jwtScheme, Array.Empty<string>() }
    });
});

// ── CORS (on-premise — Angular rodando em porta diferente) ────────────────────
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular", policy =>
        policy.WithOrigins(
                builder.Configuration.GetSection("AllowedOrigins").Get<string[]>()
                ?? ["http://localhost:4200"])
              .AllowAnyHeader()
              .AllowAnyMethod());
});

// ── Forwarded headers (TLS termina no proxy; Kestrel recebe HTTP) ─────────────
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // Confiar em qualquer origem permitiria forjar X-Forwarded-For e burlar o rate limit por IP.
    // Confia apenas em proxies de redes privadas (além do loopback, que já vem por padrão) e em 1 salto.
    o.ForwardLimit = 1;
    foreach (var (prefix, length) in new[] { ("10.0.0.0", 8), ("172.16.0.0", 12), ("192.168.0.0", 16) })
        o.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(IPAddress.Parse(prefix), length));
});

// ── Rate limiting (login — fixed window por IP) ───────────────────────────────
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("login", httpContext =>
    {
        // Config lida de forma lazy (na criação da partição) para que overrides de teste valham
        var section = httpContext.RequestServices
            .GetRequiredService<IConfiguration>().GetSection("RateLimiting:Login");
        var permitLimit = int.TryParse(section["PermitLimit"], out var p) && p > 0 ? p : 10;
        var windowSeconds = int.TryParse(section["WindowSeconds"], out var w) && w > 0 ? w : 60;

        // TestServer não tem RemoteIpAddress — fallback para "unknown"
        var key = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permitLimit,
            Window = TimeSpan.FromSeconds(windowSeconds),
            QueueLimit = 0
        });
    });

    options.OnRejected = async (context, ct) =>
    {
        var response = context.HttpContext.Response;
        response.StatusCode = StatusCodes.Status429TooManyRequests;
        response.ContentType = "application/json";

        var payload = JsonSerializer.Serialize(new
        {
            status = StatusCodes.Status429TooManyRequests,
            error = nameof(HttpStatusCode.TooManyRequests),
            message = "Muitas tentativas. Tente novamente em instantes.",
            traceId = context.HttpContext.TraceIdentifier
        });
        await response.WriteAsync(payload, ct);
    };
});

var app = builder.Build();

// ── Middleware pipeline ───────────────────────────────────────────────────────
app.UseForwardedHeaders();
app.UseMiddleware<ExceptionMiddleware>();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Personal Finance API v1");
        options.RoutePrefix = string.Empty; // Swagger na raiz
    });
}

app.UseCors("AllowAngular");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

// Torna Program acessível para WebApplicationFactory nos testes de integração
public partial class Program { }
