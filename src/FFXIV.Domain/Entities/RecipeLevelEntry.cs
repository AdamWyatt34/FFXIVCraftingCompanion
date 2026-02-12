namespace FFXIV.Domain.Entities;

public class RecipeLevelEntry
{
    public int RecipeLevel { get; set; }        // PK (the rlvl value, e.g., 1-700+)
    public int ClassJobLevel { get; set; }
    public int Stars { get; set; }
    public int Difficulty { get; set; }         // Base (unfactored) value
    public int Quality { get; set; }            // Base (unfactored) value
    public int Durability { get; set; }         // Base (unfactored) value
    public int SuggestedCraftsmanship { get; set; }
    public int SuggestedControl { get; set; }
    public int ProgressDivider { get; set; }
    public int ProgressModifier { get; set; }
    public int QualityDivider { get; set; }
    public int QualityModifier { get; set; }
}
