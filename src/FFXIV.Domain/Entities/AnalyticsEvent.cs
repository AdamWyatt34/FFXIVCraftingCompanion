namespace FFXIV.Domain.Entities;

public class AnalyticsEvent
{
    public long Id { get; set; }
    public string Event { get; set; } = string.Empty;
    public string? Metadata { get; set; }
    public string? PageUrl { get; set; }
    public string? Referrer { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
