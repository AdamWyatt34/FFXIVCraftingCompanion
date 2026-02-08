using FFXIV.Domain.Entities;
using FFXIV.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace FFXIV.Infrastructure.Persistence.Repositories;

public class ItemSourceRepository : IItemSourceRepository
{
    private readonly AppDbContext _db;

    public ItemSourceRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<ItemSource>> GetByItemIdAsync(
        int itemId, CancellationToken cancellationToken = default)
    {
        return await _db.ItemSources
            .AsNoTracking()
            .Where(s => s.ItemId == itemId)
            .ToListAsync(cancellationToken);
    }

    public async Task<Dictionary<int, List<ItemSource>>> GetByItemIdsAsync(
        IEnumerable<int> itemIds, CancellationToken cancellationToken = default)
    {
        var idList = itemIds.ToList();
        var sources = await _db.ItemSources
            .AsNoTracking()
            .Where(s => idList.Contains(s.ItemId))
            .ToListAsync(cancellationToken);

        return sources.GroupBy(s => s.ItemId)
            .ToDictionary(g => g.Key, g => g.ToList());
    }
}
