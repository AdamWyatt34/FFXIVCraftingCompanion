using FFXIV.Domain.CraftingEngine.Models;

namespace FFXIV.Domain.CraftingEngine.Solver.Models;

public enum RecipeDifficulty
{
    Trivial,
    Easy,
    Standard,
    Hard,
    Expert
}

public static class RecipeDifficultyClassifier
{
    public static RecipeDifficulty Classify(RecipeParameters recipe, CrafterStats stats)
    {
        if (recipe.IsExpert)
            return RecipeDifficulty.Expert;

        if (stats.Level >= recipe.ClassJobLevel + 10 && recipe.Stars == 0)
            return RecipeDifficulty.Trivial;

        if (stats.Level >= recipe.ClassJobLevel + 5 || recipe.Stars == 0)
            return RecipeDifficulty.Easy;

        if (recipe.Stars >= 3 || recipe.Durability <= 40)
            return RecipeDifficulty.Hard;

        return RecipeDifficulty.Standard;
    }

    public static int GetAdaptiveTimeBudgetMs(RecipeDifficulty difficulty) => difficulty switch
    {
        RecipeDifficulty.Trivial => 1000,
        RecipeDifficulty.Easy => 3000,
        RecipeDifficulty.Standard => 5000,
        RecipeDifficulty.Hard => 10000,
        RecipeDifficulty.Expert => 15000,
        _ => 10000,
    };
}
