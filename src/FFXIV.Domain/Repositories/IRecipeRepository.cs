using FFXIV.Domain.Entities;

namespace FFXIV.Domain.Repositories;

public interface IRecipeRepository
{
    Task<Recipe?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Recipe?> GetByResultItemIdAsync(int itemId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Recipe>> GetByResultItemIdsAsync(IEnumerable<int> itemIds, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<Recipe> Recipes, int TotalCount)> SearchAsync(string query, int limit, int offset, CancellationToken cancellationToken = default);
}
