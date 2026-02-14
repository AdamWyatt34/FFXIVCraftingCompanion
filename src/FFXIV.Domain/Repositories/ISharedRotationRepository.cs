using FFXIV.Domain.Entities;
using FFXIV.Domain.Enums;

namespace FFXIV.Domain.Repositories;

public interface ISharedRotationRepository
{
    Task<SharedRotation?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<SharedRotation> Items, int TotalCount)> SearchAsync(
        int? recipeId = null,
        string? craftType = null,
        int? minLevel = null,
        int? maxLevel = null,
        RotationSource? source = null,
        string sortBy = "recent",
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);

    Task<SharedRotation> CreateAsync(SharedRotation rotation, CancellationToken cancellationToken = default);
    Task DeleteAsync(SharedRotation rotation, CancellationToken cancellationToken = default);
    Task<int> GetCountByUserAsync(string userId, CancellationToken cancellationToken = default);

    // Vote operations
    Task<bool> HasVotedAsync(string userId, int rotationId, CancellationToken cancellationToken = default);
    Task AddVoteAsync(RotationVote vote, CancellationToken cancellationToken = default);
    Task RemoveVoteAsync(string userId, int rotationId, CancellationToken cancellationToken = default);
    Task IncrementUpvotesAsync(int rotationId, int delta, CancellationToken cancellationToken = default);

    // For author name lookup
    Task<string?> GetAuthorNameAsync(string userId, CancellationToken cancellationToken = default);
    Task<Dictionary<string, string>> GetAuthorNamesAsync(IEnumerable<string> userIds, CancellationToken cancellationToken = default);
}
