using FFXIV.Domain.Entities;

namespace FFXIV.Domain.Repositories;

public interface IItemSourceRepository
{
    Task<IReadOnlyList<ItemSource>> GetByItemIdAsync(int itemId, CancellationToken cancellationToken = default);
    Task<Dictionary<int, List<ItemSource>>> GetByItemIdsAsync(IEnumerable<int> itemIds, CancellationToken cancellationToken = default);
}
