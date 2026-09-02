using System.Net;
using System.Text.Json;
using Cia.Api.Exceptions;

namespace Cia.Api.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await WriteErrorAsync(context, ex);
        }
    }

    private async Task WriteErrorAsync(HttpContext context, Exception exception)
    {
        var (status, message) = exception switch
        {
            ValidationAppException => (HttpStatusCode.BadRequest, exception.Message),
            UnauthorizedAppException => (HttpStatusCode.Unauthorized, exception.Message),
            NotFoundException => (HttpStatusCode.NotFound, exception.Message),
            ConflictException => (HttpStatusCode.Conflict, exception.Message),
            _ => (HttpStatusCode.InternalServerError, "Ocorreu um erro interno. Tente novamente.")
        };

        if ((int)status >= 500)
        {
            _logger.LogError(exception, "Unhandled exception");
        }
        else
        {
            _logger.LogWarning(exception, "Handled application exception");
        }

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)status;

        var payload = new Dictionary<string, object?>
        {
            ["status"] = (int)status,
            ["error"] = message
        };

        if (_environment.IsDevelopment() && (int)status >= 500)
        {
            payload["detail"] = exception.Message;
        }

        await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
    }
}
