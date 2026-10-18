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
        RateLimitPartition.Get(ClientKey(context), _ => new RetryAfterLimiter(
            new SlidingWindowRateLimiter(new SlidingWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = window,
                SegmentsPerWindow = 5,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }),
            window));

    /// <summary>
    /// The signed-in user when there is one, else the client address as resolved by the
    /// forwarded-headers middleware. Never null: anonymous traffic without an address shares one
    /// bucket instead of bypassing the limit.
    /// </summary>
    internal static string ClientKey(HttpContext context) =>
        context.User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? context.Connection.RemoteIpAddress?.ToString()
        ?? "anonymous";

    /// <summary>
    /// A rejected sliding-window lease names <see cref="MetadataName.RetryAfter"/> among its
    /// metadata but carries no value (the fixed-window one does), so the middleware's rejection
    /// handler would send no Retry-After for these policies. This supplies the window, the longest
    /// a caller can need to wait for a permit to slide out.
    /// </summary>
    private sealed class RetryAfterLimiter(RateLimiter inner, TimeSpan window) : RateLimiter
    {
        public override TimeSpan? IdleDuration => inner.IdleDuration;

        public override RateLimiterStatistics? GetStatistics() => inner.GetStatistics();

        protected override RateLimitLease AttemptAcquireCore(int permitCount) =>
            WithRetryAfter(inner.AttemptAcquire(permitCount));

        protected override async ValueTask<RateLimitLease> AcquireAsyncCore(int permitCount, CancellationToken cancellationToken) =>
            WithRetryAfter(await inner.AcquireAsync(permitCount, cancellationToken));

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                inner.Dispose();
            base.Dispose(disposing);
        }

        private RateLimitLease WithRetryAfter(RateLimitLease lease) =>
            lease.IsAcquired || lease.TryGetMetadata(MetadataName.RetryAfter, out _)
                ? lease
                : new RejectedLease(lease, window);

        private sealed class RejectedLease(RateLimitLease inner, TimeSpan retryAfter) : RateLimitLease
        {
            public override bool IsAcquired => false;

            public override IEnumerable<string> MetadataNames =>
                inner.MetadataNames.Append(MetadataName.RetryAfter.Name).Distinct();

            public override bool TryGetMetadata(string metadataName, out object? metadata)
            {
                if (metadataName == MetadataName.RetryAfter.Name)
                {
                    metadata = retryAfter;
                    return true;
                }

                return inner.TryGetMetadata(metadataName, out metadata);
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                    inner.Dispose();
                base.Dispose(disposing);
            }
        }
    }
}
