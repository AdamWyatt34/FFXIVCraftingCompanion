using FFXIV.Domain.Entities;

namespace FFXIV.Domain.Repositories;

public interface IRecipeLevelRepository
{
    Task<RecipeLevelEntry?> GetByLevelAsync(int recipeLevel, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RecipeLevelEntry>> GetAllAsync(CancellationToken cancellationToken = default);
}
