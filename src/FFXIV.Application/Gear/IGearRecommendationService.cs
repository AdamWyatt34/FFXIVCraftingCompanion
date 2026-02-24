using FFXIV.Domain.Enums;
using FFXIV.Domain.Models;

namespace FFXIV.Application.Gear;

public interface IGearRecommendationService
{
    Task<GearSet> GetRecommendationsAsync(
        string job, int level, string profile = "balanced",
        bool craftedOnly = false, CancellationToken ct = default);

    Task<GearSlotRecommendation> GetSlotRecommendationAsync(
        EquipSlot slot, string job, int level, string profile = "balanced",
        CancellationToken ct = default);

    Task<GearStats> CalculateSetStatsAsync(
        Dictionary<EquipSlot, int> equipmentIds,
        CancellationToken ct = default);

    List<StatWeightProfile> GetAvailableProfiles(string job);
}
