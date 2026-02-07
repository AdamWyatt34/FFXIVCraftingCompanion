using FFXIV.Domain.Entities;

namespace FFXIV.Domain.Repositories;

public interface IItemRepository
{
    Task<Item?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<Item> Items, int TotalCount)> SearchByNameAsync(string query, int limit, int offset, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Item>> GetByIdsAsync(IEnumerable<int> ids, CancellationToken cancellationToken = default);
}
