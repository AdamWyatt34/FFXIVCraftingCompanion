using FFXIV.Domain.Entities;
using FFXIV.Domain.Enums;
using FFXIV.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace FFXIV.Infrastructure.Persistence.Repositories;

public class ItemRepository : IItemRepository
{
    private readonly AppDbContext _db;

    public ItemRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<Item?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _db.Items
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.ItemId == id, cancellationToken);
    }

    public async Task<(IReadOnlyList<Item> Items, int TotalCount)> SearchByNameAsync(
        string? query, int limit, int offset, CraftType? craftType = null, CancellationToken cancellationToken = default)
    {
        IQueryable<Item> baseQuery = _db.Items.AsNoTracking();

        // Filter by craft type via join with Recipes
        if (craftType.HasValue)
        {
            baseQuery = baseQuery
                .Where(i => _db.Recipes.Any(r => r.ResultItemId == i.ItemId && r.CraftType == craftType.Value));
        }

        // Filter by name if query provided
        if (!string.IsNullOrWhiteSpace(query))
        {
            var pattern = $"%{query}%";
            baseQuery = baseQuery.Where(i => EF.Functions.ILike(i.Name, pattern));
        }

        var totalCount = await baseQuery.CountAsync(cancellationToken);

        IOrderedQueryable<Item> orderedQuery;
        if (!string.IsNullOrWhiteSpace(query))
        {
            var startsWithPattern = $"{query}%";
            orderedQuery = baseQuery
                .OrderBy(i => EF.Functions.ILike(i.Name, startsWithPattern) ? 0 : 1)
                .ThenBy(i => i.Name);
        }
        else
        {
            orderedQuery = baseQuery.OrderBy(i => i.Name);
        }

        var items = await orderedQuery
            .Skip(offset)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<IReadOnlyList<Item>> GetByIdsAsync(
        IEnumerable<int> ids, CancellationToken cancellationToken = default)
    {
        var idList = ids.ToList();
        return await _db.Items
            .AsNoTracking()
            .Where(i => idList.Contains(i.ItemId))
            .ToListAsync(cancellationToken);
    }
}
