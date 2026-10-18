using FFXIV.Domain.Entities;
using FFXIV.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace FFXIV.Infrastructure.Persistence.Repositories;

public class FavoriteRecipeRepository : IFavoriteRecipeRepository
{
    private readonly AppDbContext _db;

    public FavoriteRecipeRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<FavoriteRecipe>> GetByUserAsync(
        string userId, CancellationToken cancellationToken = default)
    {
        return await _db.FavoriteRecipes
            .AsNoTracking()
            .Include(f => f.Recipe)
                .ThenInclude(r => r.ResultItem)
            .Where(f => f.UserId == userId)
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> IsFavoritedAsync(
        string userId, int recipeId, CancellationToken cancellationToken = default)
    {
        return await _db.FavoriteRecipes
            .AnyAsync(f => f.UserId == userId && f.RecipeId == recipeId, cancellationToken);
    }

    public async Task<bool> AddAsync(
        FavoriteRecipe favorite, CancellationToken cancellationToken = default)
    {
        _db.FavoriteRecipes.Add(favorite);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            // The (UserId, RecipeId) index is unique: a concurrent toggle already stored it.
            _db.Entry(favorite).State = EntityState.Detached;
            if (await IsFavoritedAsync(favorite.UserId, favorite.RecipeId, cancellationToken))
                return false;
            throw;
        }
    }

    public async Task<bool> RemoveAsync(
        string userId, int recipeId, CancellationToken cancellationToken = default)
    {
        var removed = await _db.FavoriteRecipes
            .Where(f => f.UserId == userId && f.RecipeId == recipeId)
            .ExecuteDeleteAsync(cancellationToken);
        return removed > 0;
    }

    public async Task<int> GetCountByUserAsync(
        string userId, CancellationToken cancellationToken = default)
    {
        return await _db.FavoriteRecipes.CountAsync(f => f.UserId == userId, cancellationToken);
    }
}
