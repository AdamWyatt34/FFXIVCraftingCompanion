using FFXIV.Domain.Entities;
using FFXIV.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace FFXIV.Infrastructure.Persistence.Repositories;

public class VendorInfoRepository : IVendorInfoRepository
{
    private readonly AppDbContext _db;

    public VendorInfoRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<VendorInfo>> GetByItemIdAsync(
        int itemId, CancellationToken cancellationToken = default)
    {
        return await _db.VendorInfos
            .AsNoTracking()
            .Where(v => v.ItemId == itemId)
            .ToListAsync(cancellationToken);
    }

    public async Task<Dictionary<int, List<VendorInfo>>> GetByItemIdsAsync(
        IEnumerable<int> itemIds, CancellationToken cancellationToken = default)
    {
        var idList = itemIds.ToList();
        var infos = await _db.VendorInfos
            .AsNoTracking()
            .Where(v => idList.Contains(v.ItemId))
            .ToListAsync(cancellationToken);

        return infos.GroupBy(v => v.ItemId)
            .ToDictionary(g => g.Key, g => g.ToList());
    }
}
