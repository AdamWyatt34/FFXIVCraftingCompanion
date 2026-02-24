namespace FFXIV.Domain.Models;

public class GearUpgradeSuggestion
{
    public string Slot { get; set; } = string.Empty;
    public string? CurrentItem { get; set; }
    public string SuggestedItem { get; set; } = string.Empty;
    public int SuggestedEquipmentId { get; set; }
    public int CraftsmanshipGain { get; set; }
    public int ControlGain { get; set; }
    public int CPGain { get; set; }
    public string? Source { get; set; }
    public int? RecipeId { get; set; }
}
