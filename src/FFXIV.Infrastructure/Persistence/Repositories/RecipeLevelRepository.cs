using FFXIV.Domain.Entities;
using FFXIV.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace FFXIV.Infrastructure.Persistence.Repositories;

public class RecipeLevelRepository : IRecipeLevelRepository
{
    private readonly AppDbContext _db;
    private readonly IMemoryCache _cache;
    private const string CacheKey = "RecipeLevelEntries_All";

    public RecipeLevelRepository(AppDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<RecipeLevelEntry?> GetByLevelAsync(
        int recipeLevel, CancellationToken cancellationToken = default)
    {
        var all = await GetAllCachedAsync(cancellationToken);
        return all.TryGetValue(recipeLevel, out var entry) ? entry : null;
    }

    public async Task<IReadOnlyList<RecipeLevelEntry>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        var dict = await GetAllCachedAsync(cancellationToken);
        return dict.Values.ToList().AsReadOnly();
    }

    private async Task<Dictionary<int, RecipeLevelEntry>> GetAllCachedAsync(
        CancellationToken cancellationToken)
    {
        return await _cache.GetOrCreateAsync(CacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24);
            return await _db.Set<RecipeLevelEntry>()
                .AsNoTracking()
                .ToDictionaryAsync(r => r.RecipeLevel, cancellationToken);
        }) ?? new Dictionary<int, RecipeLevelEntry>();
    }
}
