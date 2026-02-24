namespace FFXIV.Domain.Models;

public class CraftingJourneyResult
{
    public int RecipeId { get; set; }
    public MaterialTreeResult Materials { get; set; } = null!;
    public StatSufficiencyResult? Sufficiency { get; set; }
    public List<GearUpgradeSuggestion> GearUpgrades { get; set; } = [];
    public ContextualLinks ItemContext { get; set; } = null!;
}
