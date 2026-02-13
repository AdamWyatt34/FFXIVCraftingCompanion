using FFXIV.Domain.CraftingEngine.Models;
using FFXIV.Domain.CraftingEngine.Solver.Models;

namespace FFXIV.Application.CraftingEngine.Solver;

public interface ISolverCacheService
{
    Task<SolverResult?> GetCachedResultAsync(
        int recipeId, CrafterStats stats, SolverConstraints constraints,
        CancellationToken ct = default);

    Task CacheResultAsync(
        int recipeId, CrafterStats stats, SolverConstraints constraints,
        SolverResult result, CancellationToken ct = default);

    string GenerateCacheKey(int recipeId, CrafterStats stats, SolverConstraints constraints);
}
