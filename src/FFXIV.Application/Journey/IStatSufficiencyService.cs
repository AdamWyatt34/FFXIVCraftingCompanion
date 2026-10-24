using FFXIV.Domain.Models;
using FFXIV.Domain.Entities;

namespace FFXIV.Application.Journey;

public interface IStatSufficiencyService
{
    Task<StatSufficiencyResult> AnalyzeAsync(
        int recipeId,
        CrafterStats stats,
        CharacterProfile? profile = null,
        CancellationToken ct = default);
}
