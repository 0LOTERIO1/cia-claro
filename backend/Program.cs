using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using System.Security.Claims;
using Cia.Api.Configuration;
using Cia.Api.Data;
using Cia.Api.Interfaces;
using Cia.Api.Middleware;
using Cia.Api.Repositories;
using Cia.Api.Services;
using Cia.Api.Services.Knowledge;
using Cia.Api.Services.Understanding;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);
}

builder.Services.Configure<AiOptions>(builder.Configuration.GetSection(AiOptions.SectionName));
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<TelegramOptions>(builder.Configuration.GetSection(TelegramOptions.SectionName));
builder.Services.Configure<DemoUsersOptions>(builder.Configuration.GetSection(DemoUsersOptions.SectionName));
builder.Services.AddOptions<TwoFactorOptions>()
    .Bind(builder.Configuration.GetSection(TwoFactorOptions.SectionName))
    .Validate(options =>
    {
        try
        {
            return Convert.FromBase64String(options.EncryptionKey).Length == 32;
        }
        catch (FormatException)
        {
            return false;
        }
    }, "TwoFactor:EncryptionKey deve ser uma chave Base64 de 32 bytes.")
    .ValidateOnStart();

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? new[]
    {
        "http://localhost:5173",
        "http://127.0.0.1:5173",
        "http://localhost:5174",
        "http://127.0.0.1:5174"
    };

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy.SetIsOriginAllowed(origin =>
            {
                if (allowedOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
                {
                    return false;
                }

                var isLocalHttp = uri.Scheme == Uri.UriSchemeHttp
                    && (uri.Host is "localhost" or "127.0.0.1");

                var isVercel = uri.Scheme == Uri.UriSchemeHttps
                    && uri.Host.EndsWith(".vercel.app", StringComparison.OrdinalIgnoreCase);

                return isLocalHttp || isVercel;
            })
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
    options.AddPolicy("chat", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? context.User.FindFirstValue("sub")
                ?? context.Connection.RemoteIpAddress?.ToString()
                ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
    options.AddPolicy("telegram", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 300,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
    options.AddPolicy("handoff", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? context.User.FindFirstValue("sub")
                ?? context.Connection.RemoteIpAddress?.ToString()
                ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromHours(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "CIA API",
        Version = "v1",
        Description = "API da CIA — Claro Inteligência Artificial"
    });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Informe o token JWT obtido em /api/auth/login."
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
if (builder.Environment.IsProduction() &&
    (jwtOptions.Key.Length < 32 || jwtOptions.Key.Contains("change-me", StringComparison.OrdinalIgnoreCase)))
{
    throw new InvalidOperationException("Jwt:Key deve ser um segredo forte configurado no ambiente de produção.");
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key)),
            RoleClaimType = System.Security.Claims.ClaimTypes.Role,
            NameClaimType = System.Security.Claims.ClaimTypes.Name
        };
    });
builder.Services.AddAuthorization(options =>
{
    options.DefaultPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder(
            JwtBearerDefaults.AuthenticationScheme)
        .RequireAuthenticatedUser()
        .RequireClaim("amr", "mfa")
        .Build();
});

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' não configurada.");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
builder.Services.AddScoped<ISessionRepository, SessionRepository>();
builder.Services.AddScoped<IMessageRepository, MessageRepository>();
builder.Services.AddScoped<IContextRepository, ContextRepository>();
builder.Services.AddScoped<IHandoffRepository, HandoffRepository>();
builder.Services.AddScoped<ITransferRepository, TransferRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IHumanAgentRequestRepository, HumanAgentRequestRepository>();
builder.Services.AddScoped<IChannelIdentityRepository, ChannelIdentityRepository>();
builder.Services.AddScoped<IChannelLinkCodeRepository, ChannelLinkCodeRepository>();
builder.Services.AddScoped<IAccessibilityPreferencesRepository, AccessibilityPreferencesRepository>();
builder.Services.AddScoped<IServiceRatingRepository, ServiceRatingRepository>();
builder.Services.AddScoped<ITwoFactorRepository, TwoFactorRepository>();
builder.Services.AddScoped<IRegionalOutageRepository, RegionalOutageRepository>();

builder.Services.AddScoped<IIntentService, IntentService>();
builder.Services.AddScoped<IContextService, ContextService>();
builder.Services.AddScoped<IKnowledgeService, LocalKnowledgeService>();
builder.Services.AddScoped<ConversationGuardrails>();
builder.Services.AddScoped<IConversationUnderstandingService, ConversationUnderstandingService>();
builder.Services.AddSingleton<PromptSecurityService>();
builder.Services.AddSingleton<SensitiveDataRedactor>();
builder.Services.AddSingleton<TelegramAbuseGuard>();
builder.Services.AddScoped<IProtocolService, ProtocolService>();
builder.Services.AddScoped<IOrchestrationService, OrchestrationService>();
builder.Services.AddScoped<IConversationService, ConversationService>();
builder.Services.AddScoped<IHandoffService, HandoffService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<TwoFactorProtector>();
builder.Services.AddScoped<TwoFactorCodeService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IAccessibilityService, AccessibilityService>();
builder.Services.AddScoped<IHumanAgentService, HumanAgentService>();
builder.Services.AddScoped<IChannelIdentityService, ChannelIdentityService>();
builder.Services.AddScoped<IServiceRatingService, ServiceRatingService>();
builder.Services.AddScoped<IRegionalOutageService, RegionalOutageService>();
builder.Services.AddScoped<ISessionLifecycleService, SessionLifecycleService>();
builder.Services.AddScoped<ITelegramService, TelegramService>();
builder.Services.AddScoped<TelegramCommandHandler>();
builder.Services.AddScoped<ITelegramInboundService, TelegramInboundService>();
builder.Services.AddHttpClient("Telegram");
builder.Services.AddScoped<IAiService, AiService>();
builder.Services.AddScoped<LocalFallbackAiProvider>();
builder.Services.AddHttpClient<ExternalAiProvider>((sp, client) =>
{
    var timeout = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<AiOptions>>().Value.EffectiveTimeoutSeconds;
    client.Timeout = TimeSpan.FromSeconds(timeout + 2);
});

builder.Services.AddScoped<IAiProvider>(sp =>
{
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<AiOptions>>().Value;
    return options.HasExternalKey
        ? sp.GetRequiredService<ExternalAiProvider>()
        : sp.GetRequiredService<LocalFallbackAiProvider>();
});

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "CIA API v1");
        options.RoutePrefix = "swagger";
    });
}

app.UseCors("Frontend");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var demoUsers = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<DemoUsersOptions>>().Value;
    await db.Database.MigrateAsync();
    await DbSeeder.SeedAsync(db, demoUsers, app.Environment.IsProduction(), logger);
    logger.LogInformation("Database migrated and seed applied.");
}

app.Run();

public partial class Program;
