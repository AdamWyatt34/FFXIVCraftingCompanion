using FFXIV.Domain.Models;

namespace FFXIV.Application.Recipes;

public interface ITreeValidationService
{
    Task<TreeValidationResult> ValidateTreeAsync(int recipeId, int quantity = 1, CancellationToken ct = default);
    Task<BatchValidationResult> ValidateBatchAsync(IEnumerable<int> recipeIds, CancellationToken ct = default);
}
