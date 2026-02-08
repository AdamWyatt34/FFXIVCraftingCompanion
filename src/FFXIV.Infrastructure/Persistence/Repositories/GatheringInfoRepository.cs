using FFXIV.Domain.Entities;
using FFXIV.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace FFXIV.Infrastructure.Persistence.Repositories;

public class GatheringInfoRepository : IGatheringInfoRepository
{
    private readonly AppDbContext _db;

    public GatheringInfoRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<GatheringInfo>> GetByItemIdAsync(
        int itemId, CancellationToken cancellationToken = default)
    {
        return await _db.GatheringInfos
            .AsNoTracking()
            .Where(g => g.ItemId == itemId)
            .ToListAsync(cancellationToken);
    }

    public async Task<Dictionary<int, List<GatheringInfo>>> GetByItemIdsAsync(
        IEnumerable<int> itemIds, CancellationToken cancellationToken = default)
    {
        var idList = itemIds.ToList();
        var infos = await _db.GatheringInfos
            .AsNoTracking()
            .Where(g => idList.Contains(g.ItemId))
            .ToListAsync(cancellationToken);

        return infos.GroupBy(g => g.ItemId)
            .ToDictionary(g => g.Key, g => g.ToList());
    }
}
