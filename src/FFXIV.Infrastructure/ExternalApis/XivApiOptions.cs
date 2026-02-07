namespace FFXIV.Infrastructure.ExternalApis;

/// <summary>
/// Configuration options for the XIVAPI v2 client, bound from the "XivApi" config section.
/// </summary>
public sealed class XivApiOptions
{
    public const string SectionName = "XivApi";

    public string BaseUrl { get; set; } = "https://v2.xivapi.com";
    public int RateLimitPermitCount { get; set; } = 15;
    public int RateLimitWindowSeconds { get; set; } = 1;
    public int DefaultPageSize { get; set; } = 500;
    public int TimeoutSeconds { get; set; } = 30;
    public int MaxRetryAttempts { get; set; } = 3;
}
