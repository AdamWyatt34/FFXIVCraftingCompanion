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

    public async Task<FavoriteRecipe> AddAsync(
        FavoriteRecipe favorite, CancellationToken cancellationToken = default)
    {
        _db.FavoriteRecipes.Add(favorite);
        await _db.SaveChangesAsync(cancellationToken);
        return favorite;
    }

    public async Task RemoveAsync(
        string userId, int recipeId, CancellationToken cancellationToken = default)
    {
        var entity = await _db.FavoriteRecipes
            .FirstOrDefaultAsync(f => f.UserId == userId && f.RecipeId == recipeId, cancellationToken);

        if (entity is not null)
        {
            _db.FavoriteRecipes.Remove(entity);
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<int> GetCountByUserAsync(
        string userId, CancellationToken cancellationToken = default)
    {
        return await _db.FavoriteRecipes.CountAsync(f => f.UserId == userId, cancellationToken);
    }
}
