using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FFXIV.Application.CraftingEngine.Solver;
using FFXIV.Domain.CraftingEngine.Models;
using FFXIV.Domain.CraftingEngine.Solver.Models;
using FFXIV.Domain.Interfaces;

namespace FFXIV.Infrastructure.Solver;

public sealed class SolverCacheService : ISolverCacheService
{
    private readonly ICacheService _cache;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(24);
    private const string CachePrefix = "solve";

    public SolverCacheService(ICacheService cache) => _cache = cache;

    public async Task<SolverResult?> GetCachedResultAsync(
        int recipeId, CrafterStats stats, SolverConstraints constraints,
        CancellationToken ct = default)
    {
        var key = GenerateCacheKey(recipeId, stats, constraints);
        return await _cache.GetAsync<SolverResult>(key, ct);
    }

    public async Task CacheResultAsync(
        int recipeId, CrafterStats stats, SolverConstraints constraints,
        SolverResult result, CancellationToken ct = default)
    {
        var key = GenerateCacheKey(recipeId, stats, constraints);
        await _cache.SetAsync(key, result, CacheTtl, ct);
    }

    public string GenerateCacheKey(
        int recipeId, CrafterStats stats, SolverConstraints constraints)
    {
        var constraintJson = JsonSerializer.Serialize(constraints);
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(constraintJson));
        var constraintHash = Convert.ToHexString(hashBytes)[..16].ToLowerInvariant();

        return $"{CachePrefix}:{recipeId}:{stats.Craftsmanship}:{stats.Control}:{stats.CP}:{stats.Level}:{constraintHash}";
    }
}
