using FFXIV.Domain.Entities;

namespace FFXIV.Domain.Repositories;

public interface IGatheringInfoRepository
{
    Task<IReadOnlyList<GatheringInfo>> GetByItemIdAsync(int itemId, CancellationToken cancellationToken = default);
    Task<Dictionary<int, List<GatheringInfo>>> GetByItemIdsAsync(IEnumerable<int> itemIds, CancellationToken cancellationToken = default);
}
