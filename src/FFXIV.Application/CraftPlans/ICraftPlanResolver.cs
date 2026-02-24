using FFXIV.Domain.Models;

namespace FFXIV.Application.CraftPlans;

public interface ICraftPlanResolver
{
    Task<CraftPlanResult?> ResolveAsync(List<CraftPlanInput> items, CancellationToken ct = default);
}

public sealed record CraftPlanInput(int RecipeId, int Quantity);
