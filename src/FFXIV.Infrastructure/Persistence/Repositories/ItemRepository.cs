using FFXIV.Domain.Entities;
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
        string query, int limit, int offset, CancellationToken cancellationToken = default)
    {
        var pattern = $"%{query}%";
        var startsWithPattern = $"{query}%";

        var baseQuery = _db.Items
            .AsNoTracking()
            .Where(i => EF.Functions.ILike(i.Name, pattern));

        var totalCount = await baseQuery.CountAsync(cancellationToken);

        var items = await baseQuery
            .OrderBy(i => EF.Functions.ILike(i.Name, startsWithPattern) ? 0 : 1)
            .ThenBy(i => i.Name)
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
