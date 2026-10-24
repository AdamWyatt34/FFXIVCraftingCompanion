namespace FFXIV.Domain.CraftingEngine.Models;

public record RecipeParameters
{
    public int RecipeId { get; init; }
    public int RecipeLevel { get; init; }
    public int Difficulty { get; init; }
    public int Quality { get; init; }
    public int Durability { get; init; }

    // From RecipeLevelTable (XIVAPI sheet)
    public int ProgressDivider { get; init; }
    public int ProgressModifier { get; init; }
    public int QualityDivider { get; init; }
    public int QualityModifier { get; init; }

    public int ClassJobLevel { get; init; } = 0;
    public int Stars { get; init; } = 0;
    public bool IsExpert { get; init; } = false;
    public int ConditionsFlag { get; init; } = 0;
    public int SuggestedCraftsmanship { get; init; }
    public int SuggestedControl { get; init; }

    public int RequiredCraftsmanship { get; init; }
    public int RequiredControl { get; init; }

    // Starting quality from HQ materials (if any)
    public int StartingQuality { get; init; } = 0;

    // HQ ingredient data
    public int MaterialQualityFactor { get; init; } = 0;
    public List<RecipeIngredientInfo> Ingredients { get; init; } = [];
}

public sealed record RecipeIngredientInfo(int ItemId, int Quantity, bool CanBeHQ, int ItemLevel);
