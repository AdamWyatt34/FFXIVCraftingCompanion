using FFXIV.Domain.Entities;

namespace FFXIV.Domain.Repositories;

public interface ICraftPlanRepository
{
    Task<CraftPlan?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<CraftPlan?> GetByIdForUserAsync(string id, string userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CraftPlan>> GetByUserAsync(string userId, CancellationToken cancellationToken = default);
    Task<CraftPlan> CreateAsync(CraftPlan craftPlan, CancellationToken cancellationToken = default);
    Task<CraftPlan> UpdateAsync(CraftPlan craftPlan, CancellationToken cancellationToken = default);
    Task DeleteAsync(CraftPlan craftPlan, CancellationToken cancellationToken = default);
    Task<int> GetCountByUserAsync(string userId, CancellationToken cancellationToken = default);
}
