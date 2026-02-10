using FFXIV.Domain.Entities;

namespace FFXIV.Domain.Repositories;

public interface IFavoriteRecipeRepository
{
    Task<IReadOnlyList<FavoriteRecipe>> GetByUserAsync(string userId, CancellationToken cancellationToken = default);
    Task<bool> IsFavoritedAsync(string userId, int recipeId, CancellationToken cancellationToken = default);
    Task<FavoriteRecipe> AddAsync(FavoriteRecipe favorite, CancellationToken cancellationToken = default);
    Task RemoveAsync(string userId, int recipeId, CancellationToken cancellationToken = default);
    Task<int> GetCountByUserAsync(string userId, CancellationToken cancellationToken = default);
}
