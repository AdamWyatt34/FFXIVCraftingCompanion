namespace FFXIV.Api.RateLimiting;

/// <summary>
/// Per-client budgets for the named rate-limit policies, bound from the <c>RateLimiting</c>
/// configuration section so an environment (or a test host) can raise or lower them.
/// </summary>
public sealed class RateLimitSettings
{
    public const string SectionName = "RateLimiting";

    /// <summary>Login, refresh and Discord callback attempts per client per five minutes.</summary>
    public int AuthPerFiveMinutes { get; set; } = 20;

    /// <summary>Registrations per client per hour (a shared NAT counts as one client).</summary>
    public int RegisterPerHour { get; set; } = 10;

    /// <summary>Anonymous reads that cost the API real work, per client per minute.</summary>
    public int PublicReadPerMinute { get; set; } = 120;

    public int FeedbackPerHour { get; set; } = 5;

    public int SolverPerMinute { get; set; } = 20;

    public int AnalyticsPerMinute { get; set; } = 60;
}
