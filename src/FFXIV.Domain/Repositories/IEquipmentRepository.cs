using FFXIV.Domain.Entities;
using FFXIV.Domain.Enums;

namespace FFXIV.Domain.Repositories;

public interface IEquipmentRepository
{
    Task<Equipment?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Equipment>> GetByIdsAsync(IEnumerable<int> ids, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Equipment>> GetByJobAndLevelAsync(string job, int level, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Equipment>> GetBySlotJobAndLevelAsync(EquipSlot slot, string job, int level, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Equipment>> GetBestBySlotAsync(EquipSlot slot, string job, int level, int limit = 5, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Equipment>> GetBestForSlotsAsync(IEnumerable<EquipSlot> slots, string job, int level, int limitPerSlot = 10, bool craftedOnly = false, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Equipment>> SearchEquipmentAsync(string query, string? job = null, EquipSlot? slot = null, int? minLevel = null, int? maxLevel = null, int limit = 50, CancellationToken cancellationToken = default);
    Task<HashSet<int>> GetExistingItemIdsAsync(IEnumerable<int> itemIds, CancellationToken cancellationToken = default);
    Task<Equipment?> GetByRecipeIdAsync(int recipeId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Equipment>> GetByNamesAsync(IEnumerable<string> names, CancellationToken cancellationToken = default);
}
