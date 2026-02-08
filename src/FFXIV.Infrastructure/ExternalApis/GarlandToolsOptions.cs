namespace FFXIV.Infrastructure.ExternalApis;

public sealed class GarlandToolsOptions
{
    public const string SectionName = "GarlandTools";

    public string BaseUrl { get; set; } = "https://garlandtools.org";
    public int RateLimitPermitCount { get; set; } = 2;
    public int RateLimitWindowSeconds { get; set; } = 1;
    public int TimeoutSeconds { get; set; } = 30;
    public int MaxRetryAttempts { get; set; } = 3;
}
