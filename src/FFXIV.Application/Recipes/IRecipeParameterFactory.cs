using FFXIV.Domain.Models;

namespace FFXIV.Application.Recipes;

public interface IRecipeParameterFactory
{
    Task<RecipeParameters?> CreateAsync(int recipeId, CancellationToken cancellationToken = default);
}
