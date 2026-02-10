using FFXIV.Domain.Entities;
using FFXIV.Domain.Enums;

namespace FFXIV.Domain.Repositories;

public interface ISavedListRepository
{
    Task<IReadOnlyList<SavedList>> GetByUserAsync(string userId, SavedListType? type = null, CancellationToken cancellationToken = default);
    Task<SavedList?> GetByIdAsync(int id, string userId, CancellationToken cancellationToken = default);
    Task<SavedList> CreateAsync(SavedList savedList, CancellationToken cancellationToken = default);
    Task<SavedList> UpdateAsync(SavedList savedList, CancellationToken cancellationToken = default);
    Task DeleteAsync(SavedList savedList, CancellationToken cancellationToken = default);
    Task<int> GetCountByUserAsync(string userId, CancellationToken cancellationToken = default);
}
