using System.Security.Claims;
using System.Threading.RateLimiting;

namespace FFXIV.Api.RateLimiting;

/// <summary>
/// Builds the per-client partitions behind every named rate-limit policy. A policy without a
/// partition is one bucket for the whole site, so a single caller could exhaust it for everyone.
/// </summary>
internal static class RateLimitPartitions
{
    public static RateLimitPartition<string> FixedWindow(HttpContext context, int permitLimit, TimeSpan window) =>
        RateLimitPartition.GetFixedWindowLimiter(ClientKey(context), _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permitLimit,
            Window = window,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0
        });

    public static RateLimitPartition<string> SlidingWindow(HttpContext context, int permitLimit, TimeSpan window) =>
        RateLimitPartition.GetSlidingWindowLimiter(ClientKey(context), _ => new SlidingWindowRateLimiterOptions
        {
            PermitLimit = permitLimit,
            Window = window,
            SegmentsPerWindow = 5,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0
        });

    /// <summary>
    /// The signed-in user when there is one, else the client address as resolved by the
    /// forwarded-headers middleware. Never null: anonymous traffic without an address shares one
    /// bucket instead of bypassing the limit.
    /// </summary>
    internal static string ClientKey(HttpContext context) =>
        context.User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? context.Connection.RemoteIpAddress?.ToString()
        ?? "anonymous";
}
