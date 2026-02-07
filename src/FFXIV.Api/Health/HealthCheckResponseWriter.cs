using System.Text.Json;
using FFXIV.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace FFXIV.Api.Health;

public static class HealthCheckResponseWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static async Task WriteResponse(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";

        DateTimeOffset? lastDataSync = null;
        try
        {
            var dbContext = context.RequestServices.GetService<AppDbContext>();
            if (dbContext is not null)
            {
                lastDataSync = await dbContext.SyncMetadata
                    .AsNoTracking()
                    .MaxAsync(s => (DateTimeOffset?)s.LastSyncedAt);
            }
        }
        catch
        {
            // Non-fatal — just omit the sync timestamp
        }

        var response = new
        {
            status = report.Status.ToString(),
            totalDuration = report.TotalDuration.ToString(),
            lastDataSync,
            components = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                duration = e.Value.Duration.ToString(),
                error = e.Value.Exception?.Message
            })
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(response, JsonOptions));
    }
}
