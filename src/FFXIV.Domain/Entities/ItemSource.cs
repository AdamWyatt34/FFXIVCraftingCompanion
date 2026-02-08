using FFXIV.Domain.Enums;

namespace FFXIV.Domain.Entities;

public class ItemSource
{
    public int ItemSourceId { get; set; }
    public int ItemId { get; set; }
    public ItemSourceType SourceType { get; set; }
    public string SourceDetails { get; set; } = "{}";

    public Item Item { get; set; } = null!;
}
