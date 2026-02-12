using FFXIV.Domain.CraftingEngine.Models;

namespace FFXIV.Application.CraftingEngine;

public interface IRecipeParameterFactory
{
    Task<RecipeParameters?> CreateAsync(int recipeId, CancellationToken cancellationToken = default);
}
