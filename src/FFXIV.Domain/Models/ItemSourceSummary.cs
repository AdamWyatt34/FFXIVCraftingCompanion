using FFXIV.Domain.Enums;

namespace FFXIV.Domain.Models;

public class ItemSourceSummary
{
    public ItemSourceType SourceType { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? ZoneName { get; set; }
    public string? NpcName { get; set; }
    public int? Cost { get; set; }
    public string? CurrencyType { get; set; }
    public int? GatheringLevel { get; set; }
    public string? GatheringClass { get; set; }
}
