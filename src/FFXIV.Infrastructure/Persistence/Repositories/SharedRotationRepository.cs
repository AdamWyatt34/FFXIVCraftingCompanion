using FFXIV.Domain.Entities;
using FFXIV.Domain.Enums;
using FFXIV.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace FFXIV.Infrastructure.Persistence.Repositories;

public class SharedRotationRepository : ISharedRotationRepository
{
    /// <summary>Shown for authors who never chose a display name; the account email is never used.</summary>
    public const string AnonymousAuthorName = "Anonymous";

    private readonly AppDbContext _db;

    public SharedRotationRepository(AppDbContext db) => _db = db;

    public async Task<SharedRotation?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _db.SharedRotations
            .Include(r => r.Recipe)
                .ThenInclude(r => r.ResultItem)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _db.SharedRotations.AnyAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<(IReadOnlyList<SharedRotation> Items, int TotalCount)> SearchAsync(
        int? recipeId = null,
        string? craftType = null,
        int? minLevel = null,
        int? maxLevel = null,
        RotationSource? source = null,
        string sortBy = "recent",
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = _db.SharedRotations
            .AsNoTracking()
            .Include(r => r.Recipe)
                .ThenInclude(r => r.ResultItem)
            .AsQueryable();

        if (recipeId.HasValue)
            query = query.Where(r => r.RecipeId == recipeId.Value);

        if (!string.IsNullOrWhiteSpace(craftType) && Enum.TryParse<CraftType>(craftType, true, out var ct))
            query = query.Where(r => r.Recipe.CraftType == ct);

        if (minLevel.HasValue)
            query = query.Where(r => r.Recipe.RecipeLevel >= minLevel.Value);

        if (maxLevel.HasValue)
            query = query.Where(r => r.Recipe.RecipeLevel <= maxLevel.Value);

        if (source.HasValue)
            query = query.Where(r => r.Source == source.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        query = sortBy switch
        {
            "quality" => query.OrderByDescending(r => r.QualityPercent).ThenByDescending(r => r.CreatedAt),
            "steps" => query.OrderBy(r => r.TotalSteps).ThenByDescending(r => r.QualityPercent),
            "popular" => query.OrderByDescending(r => r.Upvotes).ThenByDescending(r => r.CreatedAt),
            _ => query.OrderByDescending(r => r.CreatedAt),
        };

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<SharedRotation> CreateAsync(SharedRotation rotation, CancellationToken cancellationToken = default)
    {
        _db.SharedRotations.Add(rotation);
        await _db.SaveChangesAsync(cancellationToken);
        return rotation;
    }

    public async Task DeleteAsync(SharedRotation rotation, CancellationToken cancellationToken = default)
    {
        _db.SharedRotations.Remove(rotation);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> GetCountByUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _db.SharedRotations.CountAsync(r => r.UserId == userId, cancellationToken);
    }

    // Vote operations

    public async Task<bool> HasVotedAsync(string userId, int rotationId, CancellationToken cancellationToken = default)
    {
        return await _db.RotationVotes
            .AnyAsync(v => v.UserId == userId && v.RotationId == rotationId, cancellationToken);
    }

    public async Task<bool> AddVoteAsync(RotationVote vote, CancellationToken cancellationToken = default)
    {
        _db.RotationVotes.Add(vote);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            // The (UserId, RotationId) index is unique: two requests from the same user raced and
            // the other one won. The vote exists either way, so this is not an error.
            _db.Entry(vote).State = EntityState.Detached;
            if (await HasVotedAsync(vote.UserId, vote.RotationId, cancellationToken))
                return false;
            throw;
        }
    }

    public async Task<bool> RemoveVoteAsync(string userId, int rotationId, CancellationToken cancellationToken = default)
    {
        var removed = await _db.RotationVotes
            .Where(v => v.UserId == userId && v.RotationId == rotationId)
            .ExecuteDeleteAsync(cancellationToken);
        return removed > 0;
    }

    public async Task<int> RecountUpvotesAsync(int rotationId, CancellationToken cancellationToken = default)
    {
        await _db.SharedRotations
            .Where(r => r.Id == rotationId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(
                r => r.Upvotes,
                r => _db.RotationVotes.Count(v => v.RotationId == r.Id)), cancellationToken);

        return await _db.SharedRotations
            .AsNoTracking()
            .Where(r => r.Id == rotationId)
            .Select(r => r.Upvotes)
            .FirstOrDefaultAsync(cancellationToken);
    }

    // Author name lookup. UserName is the sign-in email, so only the chosen display name is shown.

    public async Task<string?> GetAuthorNameAsync(string userId, CancellationToken cancellationToken = default)
    {
        var author = await _db.Users
            .Where(u => u.Id == userId)
            .Select(u => new { u.DisplayName })
            .FirstOrDefaultAsync(cancellationToken);

        return author is null ? null : author.DisplayName ?? AnonymousAuthorName;
    }

    public async Task<Dictionary<string, string>> GetAuthorNamesAsync(
        IEnumerable<string> userIds, CancellationToken cancellationToken = default)
    {
        var ids = userIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<string, string>();

        return await _db.Users
            .Where(u => ids.Contains(u.Id))
            .ToDictionaryAsync(
                u => u.Id,
                u => u.DisplayName ?? AnonymousAuthorName,
                cancellationToken);
    }
}
