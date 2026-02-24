namespace FFXIV.Domain.Models;

public enum StatRecommendation { StatsExcellent, StatsGood, StatsAdequate, UpgradeGear }

public record StatSufficiencyResult
{
    public int RecipeId { get; init; }
    public bool MeetsMinimumRequirements { get; init; }
    public bool CraftsmanshipSufficient { get; init; }
    public bool ControlSufficient { get; init; }
    public bool LevelSufficient { get; init; }
    public int CraftsmanshipDeficit { get; init; }
    public int ControlDeficit { get; init; }
    public int LevelDeficit { get; init; }
    public int EstimatedHQPercent { get; init; }
    public double EstimatedQualityPercent { get; init; }
    public int EstimatedSteps { get; init; }
    public bool CanComplete { get; init; }
    public StatRecommendation Recommendation { get; init; }
    public List<GearUpgradeSuggestion> SuggestedGearUpgrades { get; init; } = [];
}
