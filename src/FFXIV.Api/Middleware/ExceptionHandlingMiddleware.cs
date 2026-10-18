using System.Diagnostics;
using System.Text.Json;
using FFXIV.Application.Common;
using Sentry;

namespace FFXIV.Api.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            _logger.LogInformation("Request was cancelled by the client");
            context.Response.StatusCode = 499; // Client Closed Request
        }
        catch (RequestValidationException validationEx)
        {
            // Written for the client by the handler that threw it.
            _logger.LogInformation("Rejected {Method} {Path}: {Reason}",
                context.Request.Method, context.Request.Path, validationEx.Message);

            await WriteErrorAsync(context, StatusCodes.Status400BadRequest, validationEx.Message);
        }
        catch (ArgumentException argEx)
        {
            // The message describes internals (parameter names, ids); it goes to the log, not the client.
            _logger.LogWarning(argEx, "Validation error processing {Method} {Path}",
                context.Request.Method, context.Request.Path);

            await WriteErrorAsync(context, StatusCodes.Status400BadRequest, "The request is invalid.");
        }
        catch (Exception ex)
        {
            SentrySdk.CaptureException(ex);
            _logger.LogError(ex, "Unhandled exception processing {Method} {Path}", context.Request.Method, context.Request.Path);

            await WriteErrorAsync(context, StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
        }
    }

    private static async Task WriteErrorAsync(HttpContext context, int statusCode, string message)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        var response = new
        {
            error = message,
            traceId = Activity.Current?.Id ?? context.TraceIdentifier,
            timestamp = DateTimeOffset.UtcNow
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }
}
