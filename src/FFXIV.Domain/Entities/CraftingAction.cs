namespace FFXIV.Domain.Entities;

public class CraftingAction
{
    public int ActionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int LevelRequired { get; set; }
    public int CpCost { get; set; }
    public int DurabilityCost { get; set; }
    public int ProgressPotency { get; set; }
    public int QualityPotency { get; set; }
    public string? BuffType { get; set; }
    public int? BuffDuration { get; set; }
}
