using FFXIV.Domain.Enums;

namespace FFXIV.Domain.Models;

public class GatheringGuide
{
    public List<GatheringZoneGroup> ZoneGroups { get; set; } = [];
    public List<TimedNodeEntry> TimedNodes { get; set; } = [];
    public List<CrystalRequirement> CrystalSummary { get; set; } = [];
    public GatheringGuideSummary Summary { get; set; } = new();
}

public class GatheringZoneGroup
{
    public string ZoneName { get; set; } = string.Empty;
    public List<GatheringGuideItem> Items { get; set; } = [];
}

public class GatheringGuideItem
{
    public int ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string IconUrl { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string GatheringClass { get; set; } = string.Empty;
    public int GatheringLevel { get; set; }
    public NodeType NodeType { get; set; }
    public float? CoordinateX { get; set; }
    public float? CoordinateY { get; set; }
    public EorzeanTimeWindow? TimeWindow { get; set; }
    public bool IsHidden { get; set; }
}

public class EorzeanTimeWindow
{
    public int StartHour { get; set; }
    public int EndHour { get; set; }
}

public class TimedNodeEntry
{
    public int ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string IconUrl { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string ZoneName { get; set; } = string.Empty;
    public string GatheringClass { get; set; } = string.Empty;
    public int GatheringLevel { get; set; }
    public NodeType NodeType { get; set; }
    public int StartHour { get; set; }
    public int EndHour { get; set; }
    public float? CoordinateX { get; set; }
    public float? CoordinateY { get; set; }
}

public class CrystalRequirement
{
    public int ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string IconUrl { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string Element { get; set; } = string.Empty;
    public string Tier { get; set; } = string.Empty;
}

public class GatheringGuideSummary
{
    public int TotalZones { get; set; }
    public int TotalItems { get; set; }
    public int TimedNodeCount { get; set; }
    public int TotalCrystalTypes { get; set; }
}
