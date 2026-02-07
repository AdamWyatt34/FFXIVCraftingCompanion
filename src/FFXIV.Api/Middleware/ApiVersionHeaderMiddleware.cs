namespace FFXIV.Api.Middleware;

public class ApiVersionHeaderMiddleware
{
    private readonly RequestDelegate _next;

    public ApiVersionHeaderMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            context.Response.Headers["X-Api-Version"] = "1.0";
            return Task.CompletedTask;
        });

        await _next(context);
    }
}
