using System.Text.Json.Serialization;
using Cia.Api.Configuration;
using Cia.Api.Data;
using Cia.Api.Interfaces;
using Cia.Api.Middleware;
using Cia.Api.Repositories;
using Cia.Api.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<AiOptions>(builder.Configuration.GetSection(AiOptions.SectionName));

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

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "CIA API",
        Version = "v1",
        Description = "API da CIA — Claro Inteligência Artificial"
    });
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

builder.Services.AddScoped<IIntentService, IntentService>();
builder.Services.AddScoped<IContextService, ContextService>();
builder.Services.AddScoped<IProtocolService, ProtocolService>();
builder.Services.AddScoped<IConversationService, ConversationService>();
builder.Services.AddScoped<IHandoffService, HandoffService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IAiService, AiService>();
builder.Services.AddScoped<LocalFallbackAiProvider>();
builder.Services.AddHttpClient<ExternalAiProvider>();

builder.Services.AddScoped<IAiProvider>(sp =>
{
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<AiOptions>>().Value;
    return options.HasExternalKey
        ? sp.GetRequiredService<ExternalAiProvider>()
        : sp.GetRequiredService<LocalFallbackAiProvider>();
});

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "CIA API v1");
    options.RoutePrefix = "swagger";
});

app.UseCors("Frontend");
app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    await db.Database.MigrateAsync();
    await DbSeeder.SeedAsync(db);
    logger.LogInformation("Database migrated and seed applied.");
}

app.Run();

public partial class Program;
