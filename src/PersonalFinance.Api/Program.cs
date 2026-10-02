using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using PersonalFinance.Api.Auth;
using PersonalFinance.Api.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using PersonalFinance.Api.Converters;
using PersonalFinance.Api.Extensions;
using PersonalFinance.Api.Controllers.V1;
using PersonalFinance.Api.Middleware;
using PersonalFinance.Infrastructure.Auth;
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
    // Invalidação de sessões (#488): só no esquema principal; o do challenge MFA não é afetado.
    options.Events = new JwtBearerEvents { OnTokenValidated = SecurityStampValidator.ValidateAsync };
})
// Esquema do token intermediário do 2º fator (MFA): audience própria, usado só em /auth/mfa/verify.
// O esquema padrão (audience do token completo) rejeita o challenge em qualquer [Authorize].
.AddJwtBearer(MfaVerifyController.ChallengeScheme, options =>
{
    options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
        ValidateIssuer = true,
        ValidIssuer = jwtSection["Issuer"],
        ValidateAudience = true,
        ValidAudience = JwtTokenService.MfaChallengeAudience,
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
// Validado no startup: PermitLimit/WindowSeconds <= 0 falham ao subir (sem fallback silencioso)
builder.Services.AddOptions<LoginRateLimitOptions>()
    .Bind(builder.Configuration.GetSection("RateLimiting:Login"))
    .Validate(o => o.PermitLimit > 0, "RateLimiting:Login:PermitLimit deve ser > 0.")
    .Validate(o => o.WindowSeconds > 0, "RateLimiting:Login:WindowSeconds deve ser > 0.")
    .ValidateOnStart();

// Rate limit do 2º fator (mfa-verify) — mesma validação de startup do login
builder.Services.AddOptions<MfaVerifyRateLimitOptions>()
    .Bind(builder.Configuration.GetSection("RateLimiting:MfaVerify"))
    .Validate(o => o.PermitLimit > 0, "RateLimiting:MfaVerify:PermitLimit deve ser > 0.")
    .Validate(o => o.WindowSeconds > 0, "RateLimiting:MfaVerify:WindowSeconds deve ser > 0.")
    .ValidateOnStart();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("mfa-verify", httpContext =>
    {
        var verifyOptions = httpContext.RequestServices
            .GetRequiredService<IOptions<MfaVerifyRateLimitOptions>>().Value;
        var permitLimit = verifyOptions.PermitLimit;
        var windowSeconds = verifyOptions.WindowSeconds;

        // Partição própria (prefixo) para não dividir contador com a policy do login
        var key = "mfa-verify:" + (httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");

        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permitLimit,
            Window = TimeSpan.FromSeconds(windowSeconds),
            QueueLimit = 0
        });
    });

    options.AddPolicy("login", httpContext =>
    {
        // Options resolvidas de forma lazy (já validadas no startup) para que overrides de teste valham
        var loginOptions = httpContext.RequestServices
            .GetRequiredService<IOptions<LoginRateLimitOptions>>().Value;
        var permitLimit = loginOptions.PermitLimit;
        var windowSeconds = loginOptions.WindowSeconds;

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

        // Segundos de espera: metadata do lease; fallback = janela configurada
        var retryAfterSeconds = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
            ? (int)Math.Ceiling(retryAfter.TotalSeconds)
            : context.HttpContext.RequestServices
                .GetRequiredService<IOptions<LoginRateLimitOptions>>().Value.WindowSeconds;
        retryAfterSeconds = Math.Max(1, retryAfterSeconds);
        response.Headers.RetryAfter = retryAfterSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture);

        var payload = JsonSerializer.Serialize(new
        {
            status = StatusCodes.Status429TooManyRequests,
            error = nameof(HttpStatusCode.TooManyRequests),
            message = $"Muitas tentativas. Tente novamente em {retryAfterSeconds} segundos.",
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
