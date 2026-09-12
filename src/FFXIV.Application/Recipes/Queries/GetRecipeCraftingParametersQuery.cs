using FFXIV.Application.CraftingEngine;
using FFXIV.Application.Recipes.DTOs;
using MediatR;

namespace FFXIV.Application.Recipes.Queries;

public sealed record GetRecipeCraftingParametersQuery(int RecipeId)
    : IRequest<RecipeCraftingParametersDto?>;

public sealed class GetRecipeCraftingParametersQueryHandler
    : IRequestHandler<GetRecipeCraftingParametersQuery, RecipeCraftingParametersDto?>
{
    private readonly IRecipeParameterFactory _recipeParamFactory;

    public GetRecipeCraftingParametersQueryHandler(IRecipeParameterFactory recipeParamFactory)
    {
        _recipeParamFactory = recipeParamFactory;
    }

    public async Task<RecipeCraftingParametersDto?> Handle(
        GetRecipeCraftingParametersQuery request, CancellationToken cancellationToken)
    {
        var parameters = await _recipeParamFactory.CreateAsync(request.RecipeId, cancellationToken);
        if (parameters is null)
            return null;

        return new RecipeCraftingParametersDto(
            parameters.RecipeId,
            parameters.RecipeLevel,
            parameters.Difficulty,
            parameters.Quality,
            parameters.Durability,
            parameters.ProgressDivider,
            parameters.ProgressModifier,
            parameters.QualityDivider,
            parameters.QualityModifier,
            parameters.ClassJobLevel,
            parameters.Stars,
            parameters.IsExpert,
            parameters.ConditionsFlag,
            parameters.SuggestedCraftsmanship,
            parameters.SuggestedControl,
            parameters.MaterialQualityFactor,
            parameters.Ingredients
                .Select(i => new RecipeCraftingIngredientDto(i.ItemId, i.Quantity, i.CanBeHQ))
                .ToList());
    }
}
