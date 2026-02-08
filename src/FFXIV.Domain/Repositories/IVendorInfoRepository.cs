using FFXIV.Domain.Entities;

namespace FFXIV.Domain.Repositories;

public interface IVendorInfoRepository
{
    Task<IReadOnlyList<VendorInfo>> GetByItemIdAsync(int itemId, CancellationToken cancellationToken = default);
    Task<Dictionary<int, List<VendorInfo>>> GetByItemIdsAsync(IEnumerable<int> itemIds, CancellationToken cancellationToken = default);
}
