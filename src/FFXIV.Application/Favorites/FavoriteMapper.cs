using FFXIV.Application.Favorites.DTOs;
using FFXIV.Domain.Entities;

namespace FFXIV.Application.Favorites;

public static class FavoriteMapper
{
    public static FavoriteRecipeDto ToDto(FavoriteRecipe favorite)
    {
        return new FavoriteRecipeDto(
            RecipeId: favorite.RecipeId,
            ItemName: favorite.Recipe.ResultItem.Name,
            IconUrl: favorite.Recipe.ResultItem.IconPath,
            CraftType: favorite.Recipe.CraftType.ToString(),
            RecipeLevel: favorite.Recipe.RecipeLevel,
            Note: favorite.Note,
            CreatedAt: favorite.CreatedAt);
    }
}
