using FFXIV.Domain.Enums;

namespace FFXIV.Domain.Entities;

public class GatheringInfo
{
    public int GatheringInfoId { get; set; }
    public int ItemId { get; set; }
    public GatheringType GatheringType { get; set; }
    public string ClassJob { get; set; } = string.Empty;
    public int Level { get; set; }
    public int Stars { get; set; }
    public string ZoneName { get; set; } = string.Empty;
    public string? AreaName { get; set; }
    public float? CoordinateX { get; set; }
    public float? CoordinateY { get; set; }
    public bool IsTimed { get; set; }
    public string? SpawnTimes { get; set; }
    public bool IsHidden { get; set; }
    public NodeType NodeType { get; set; } = NodeType.Standard;

    public Item Item { get; set; } = null!;
}
