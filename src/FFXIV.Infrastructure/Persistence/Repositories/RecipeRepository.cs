using FFXIV.Domain.Entities;
using FFXIV.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace FFXIV.Infrastructure.Persistence.Repositories;

public class RecipeRepository : IRecipeRepository
{
    private readonly AppDbContext _db;

    public RecipeRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<Recipe?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _db.Recipes
            .AsNoTracking()
            .Include(r => r.ResultItem)
            .Include(r => r.Ingredients)
                .ThenInclude(ri => ri.Item)
            .FirstOrDefaultAsync(r => r.RecipeId == id, cancellationToken);
    }

    public async Task<Recipe?> GetByResultItemIdAsync(int itemId, CancellationToken cancellationToken = default)
    {
        return await _db.Recipes
            .AsNoTracking()
            .Include(r => r.ResultItem)
            .Include(r => r.Ingredients)
                .ThenInclude(ri => ri.Item)
            .FirstOrDefaultAsync(r => r.ResultItemId == itemId, cancellationToken);
    }

    public async Task<IReadOnlyList<Recipe>> GetByResultItemIdsAsync(
        IEnumerable<int> itemIds, CancellationToken cancellationToken = default)
    {
        var idList = itemIds.ToList();
        return await _db.Recipes
            .AsNoTracking()
            .Include(r => r.ResultItem)
            .Include(r => r.Ingredients)
                .ThenInclude(ri => ri.Item)
            .Where(r => idList.Contains(r.ResultItemId))
            .ToListAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<Recipe> Recipes, int TotalCount)> SearchAsync(
        string query, int limit, int offset, CancellationToken cancellationToken = default)
    {
        var pattern = $"%{query}%";
        var startsWithPattern = $"{query}%";

        var baseQuery = _db.Recipes
            .AsNoTracking()
            .Include(r => r.ResultItem)
            .Include(r => r.Ingredients)
                .ThenInclude(ri => ri.Item)
            .Where(r => EF.Functions.ILike(r.ResultItem.Name, pattern));

        var totalCount = await baseQuery.CountAsync(cancellationToken);

        var recipes = await baseQuery
            .OrderBy(r => EF.Functions.ILike(r.ResultItem.Name, startsWithPattern) ? 0 : 1)
            .ThenBy(r => r.ResultItem.Name)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return (recipes, totalCount);
    }
}
