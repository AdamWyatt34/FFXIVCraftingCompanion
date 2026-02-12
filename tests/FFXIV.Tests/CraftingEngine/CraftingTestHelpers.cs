using FFXIV.Domain.CraftingEngine.Models;

namespace FFXIV.Tests.CraftingEngine;

internal static class CraftingTestHelpers
{
    public static CraftingState CreateDefaultState() => new()
    {
        Progress = 0,
        Quality = 0,
        Durability = 70,
        MaxDurability = 70,
        CP = 600,
        MaxCP = 600,
        MaxProgress = 3500,
        MaxQuality = 7200,
        Step = 0,
        Condition = CraftCondition.Normal,
        Status = CraftStatus.InProgress,
        IsFirstStep = true,
    };

    public static CrafterStats CreateDefaultStats() => new()
    {
        Level = 90,
        Craftsmanship = 3800,
        Control = 3600,
        CP = 600,
    };

    public static RecipeParameters CreateDefaultRecipe() => new()
    {
        RecipeId = 100,
        RecipeLevel = 580,
        Difficulty = 3500,
        Quality = 7200,
        Durability = 70,
        ProgressDivider = 130,
        ProgressModifier = 80,
        QualityDivider = 115,
        QualityModifier = 70,
        Stars = 2,
        SuggestedCraftsmanship = 3700,
        SuggestedControl = 3280,
    };
}
