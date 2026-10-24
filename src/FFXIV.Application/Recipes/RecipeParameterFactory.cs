using FFXIV.Domain.Models;
using FFXIV.Domain.Repositories;

namespace FFXIV.Application.Recipes;

public class RecipeParameterFactory : IRecipeParameterFactory
{
    private readonly IRecipeRepository _recipeRepository;
    private readonly IRecipeLevelRepository _recipeLevelRepository;

    public RecipeParameterFactory(
        IRecipeRepository recipeRepository,
        IRecipeLevelRepository recipeLevelRepository)
    {
        _recipeRepository = recipeRepository;
        _recipeLevelRepository = recipeLevelRepository;
    }

    public async Task<RecipeParameters?> CreateAsync(
        int recipeId, CancellationToken cancellationToken = default)
    {
        var recipe = await _recipeRepository.GetByIdAsync(recipeId, cancellationToken);
        if (recipe is null) return null;

        var rlt = await _recipeLevelRepository.GetByLevelAsync(recipe.RecipeLevel, cancellationToken);
        if (rlt is null) return null;

        var ingredients = recipe.Ingredients
            .Select(i => new RecipeIngredientInfo(i.ItemId, i.Quantity, i.Item.CanBeHQ, i.Item.ItemLevel))
            .ToList();

        return new RecipeParameters
        {
            RecipeId = recipe.RecipeId,
            RecipeLevel = rlt.RecipeLevel,
            Difficulty = recipe.Difficulty,
            Quality = recipe.Quality,
            Durability = recipe.Durability,
            ProgressDivider = rlt.ProgressDivider,
            ProgressModifier = rlt.ProgressModifier,
            QualityDivider = rlt.QualityDivider,
            QualityModifier = rlt.QualityModifier,
            ClassJobLevel = rlt.ClassJobLevel,
            Stars = rlt.Stars,
            SuggestedCraftsmanship = rlt.SuggestedCraftsmanship,
            SuggestedControl = rlt.SuggestedControl,
            RequiredCraftsmanship = recipe.RequiredCraftsmanship,
            RequiredControl = recipe.RequiredControl,
            MaterialQualityFactor = recipe.MaterialQualityFactor,
            Ingredients = ingredients,
        };
    }
}
