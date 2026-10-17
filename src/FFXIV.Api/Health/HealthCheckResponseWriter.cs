using System.Text.Json;
using FFXIV.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace FFXIV.Api.Health;

/// <summary>
/// Builds the readiness report body. The endpoint that serves it is admin-only, so component
/// descriptions and error messages are included for the operator.
/// </summary>
public static class HealthCheckResponseWriter
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static int StatusCodeFor(HealthReport report) =>
        report.Status == HealthStatus.Unhealthy
            ? StatusCodes.Status503ServiceUnavailable
            : StatusCodes.Status200OK;

    public static async Task<object> BuildResponseAsync(
        IServiceProvider services, HealthReport report, CancellationToken cancellationToken)
    {
        DateTimeOffset? lastDataSync = null;
        try
        {
            var dbContext = services.GetService<AppDbContext>();
            if (dbContext is not null)
            {
                lastDataSync = await dbContext.SyncMetadata
                    .AsNoTracking()
                    .MaxAsync(s => (DateTimeOffset?)s.LastSyncedAt, cancellationToken);
            }
        }
        catch
        {
            // Non-fatal — just omit the sync timestamp
        }

        return new
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
    }
}
