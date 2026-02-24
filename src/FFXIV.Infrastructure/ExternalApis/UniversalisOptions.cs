namespace FFXIV.Infrastructure.ExternalApis;

public sealed class UniversalisOptions
{
    public const string SectionName = "Universalis";

    public string BaseUrl { get; set; } = "https://universalis.app/api/v2";
    public int RateLimitPermitCount { get; set; } = 20;
    public int RateLimitWindowSeconds { get; set; } = 1;
    public int TimeoutSeconds { get; set; } = 30;
    public int MaxRetryAttempts { get; set; } = 3;
}
