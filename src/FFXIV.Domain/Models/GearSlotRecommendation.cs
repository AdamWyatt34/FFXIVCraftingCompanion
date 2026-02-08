using FFXIV.Domain.Entities;
using FFXIV.Domain.Enums;

namespace FFXIV.Domain.Models;

public class GearSlotRecommendation
{
    public EquipSlot Slot { get; set; }
    public string SlotLabel { get; set; } = string.Empty;
    public Equipment? RecommendedItem { get; set; }
    public List<Equipment> Alternatives { get; set; } = [];
    public EquipmentSourceInfo Source { get; set; } = new();
    public double WeightedScore { get; set; }
}
