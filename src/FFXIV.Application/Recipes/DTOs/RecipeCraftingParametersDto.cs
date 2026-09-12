namespace FFXIV.Application.Recipes.DTOs;

/// <summary>
/// The crafting engine's view of a recipe: the recipe row plus its RecipeLevelTable entry.
/// Served so a client-side engine can run the simulator and solver without the crafting endpoints.
/// </summary>
public sealed record RecipeCraftingParametersDto(
    int RecipeId,
    int RecipeLevel,
    int Difficulty,
    int Quality,
    int Durability,
    int ProgressDivider,
    int ProgressModifier,
    int QualityDivider,
    int QualityModifier,
    int ClassJobLevel,
    int Stars,
    bool IsExpert,
    int ConditionsFlag,
    int SuggestedCraftsmanship,
    int SuggestedControl,
    int MaterialQualityFactor,
    List<RecipeCraftingIngredientDto> Ingredients);

public sealed record RecipeCraftingIngredientDto(int ItemId, int Quantity, bool CanBeHQ);
