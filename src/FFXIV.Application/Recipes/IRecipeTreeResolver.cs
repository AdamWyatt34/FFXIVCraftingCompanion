using FFXIV.Domain.Models;

namespace FFXIV.Application.Recipes;

public interface IRecipeTreeResolver
{
    Task<MaterialTreeResult> ResolveTreeAsync(int itemId, int quantity = 1, CancellationToken ct = default);
}
