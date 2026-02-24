using FFXIV.Domain.CraftingEngine.Models;
using FFXIV.Domain.Entities;
using FFXIV.Domain.Models;

namespace FFXIV.Application.Journey;

public interface IStatSufficiencyService
{
    Task<StatSufficiencyResult> AnalyzeAsync(
        int recipeId,
        CrafterStats stats,
        CharacterProfile? profile = null,
        CancellationToken ct = default);
}
