using FFXIV.Domain.Entities;

namespace FFXIV.Domain.Repositories;

public interface IFavoriteRecipeRepository
{
    Task<IReadOnlyList<FavoriteRecipe>> GetByUserAsync(string userId, CancellationToken cancellationToken = default);
    Task<bool> IsFavoritedAsync(string userId, int recipeId, CancellationToken cancellationToken = default);

    /// <summary>Stores the favourite; false when the recipe was already favourited (a concurrent duplicate).</summary>
    Task<bool> AddAsync(FavoriteRecipe favorite, CancellationToken cancellationToken = default);

    /// <summary>Removes the favourite; false when there was none to remove.</summary>
    Task<bool> RemoveAsync(string userId, int recipeId, CancellationToken cancellationToken = default);
    Task<int> GetCountByUserAsync(string userId, CancellationToken cancellationToken = default);
}
